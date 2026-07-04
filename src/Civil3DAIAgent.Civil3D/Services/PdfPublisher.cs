using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Publishing;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Results;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Implements the Core <see cref="IPdfPublisher"/> port using the AutoCAD Publisher API. Publishes
    /// the given layouts of the active drawing to PDF (single multi-page file or one file per sheet).
    /// </summary>
    /// <remarks>
    /// VERSION SENSITIVITY: the <c>DsdData.SheetType</c> enum values and the Publisher plot-config name
    /// ("DWG To PDF.pc3") are the two spots most likely to need adjustment on non-2024 installs;
    /// both are tagged "// [VERSION]". Publishing is forced foreground (BACKGROUNDPLOT=0) so the call is
    /// synchronous and errors are observable.
    /// </remarks>
    public sealed class PdfPublisher : IPdfPublisher
    {
        private const string Category = "PDF";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;
        private readonly ICivilDocProvider _docProvider;

        /// <summary>Creates the publisher.</summary>
        public PdfPublisher(ILogger logger, IExceptionExplainer explainer, ICivilDocProvider docProvider)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
            _docProvider = docProvider;
        }

        /// <inheritdoc />
        public OperationResult<IReadOnlyList<string>> Publish(
            IReadOnlyList<string> layoutNames, string outputFolder, string outputFileName,
            string pageSetupName, int dpi, bool mergeToSingleFile)
        {
            if (layoutNames == null || layoutNames.Count == 0)
                return OperationResult<IReadOnlyList<string>>.Fail("There are no layouts to publish.");

            var doc = _docProvider?.ActiveDocument ?? AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return OperationResult<IReadOnlyList<string>>.Fail("No active drawing to publish.");

            string tempDwg = null;
            object previousBgPlot = null;
            try
            {
                Directory.CreateDirectory(outputFolder);

                // 1) The Publisher reads layouts from a DWG on disk, so ensure the drawing is saved.
                string dwgPath = EnsureSavedToDisk(doc, outputFolder, out tempDwg);

                // 2) Force synchronous (foreground) publishing so we can detect success/failure.
                previousBgPlot = AcApp.GetSystemVariable("BACKGROUNDPLOT");
                AcApp.SetSystemVariable("BACKGROUNDPLOT", 0);

                // 3) Build the DSD entry list.
                string destBase = ResolveDestination(outputFolder, outputFileName, doc);
                var entries = new DsdEntryCollection();
                foreach (var layout in layoutNames)
                {
                    var entry = new DsdEntry
                    {
                        DwgName = dwgPath,
                        Layout = layout,
                        Title = layout,
                        Nps = pageSetupName ?? string.Empty,
                        NpsSourceDwg = string.IsNullOrEmpty(pageSetupName) ? string.Empty : dwgPath
                    };
                    entries.Add(entry);
                }

                var dsd = new DsdData
                {
                    // [VERSION] Single multi-page PDF vs one PDF per sheet.
                    SheetType = mergeToSingleFile ? SheetType.SinglePdf : SheetType.MultiPdf,
                    ProjectPath = outputFolder,
                    DestinationName = destBase,
                    NoOfCopies = 1,
                    IsHomogeneous = false,
                    PromptForDwfName = false
                };
                dsd.SetDsdEntryCollection(entries);

                // 4) Publish with the PDF plot config.
                var pdfFilesBefore = new HashSet<string>(SafeEnumeratePdfs(outputFolder), StringComparer.OrdinalIgnoreCase);

                // [VERSION] The stock PDF plotter configuration.
                PlotConfig plotConfig = PlotConfigManager.SetCurrentConfig("DWG To PDF.pc3");

                var publisher = AcApp.Publisher;
                publisher.PublishExecute(dsd, plotConfig);

                // 5) Determine which PDFs were produced.
                var produced = SafeEnumeratePdfs(outputFolder)
                    .Where(f => !pdfFilesBefore.Contains(f))
                    .ToList();

                if (produced.Count == 0 && File.Exists(destBase))
                    produced.Add(destBase);

                if (produced.Count == 0)
                    return OperationResult<IReadOnlyList<string>>.Fail(
                        "Publishing completed but no PDF file was found. Check that the 'DWG To PDF.pc3' " +
                        "plotter is installed and the output folder is writable.");

                _logger.Info($"Published {produced.Count} PDF file(s) to {outputFolder}.", Category);
                return OperationResult<IReadOnlyList<string>>.Ok(produced, "PDF(s) generated.");
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<string>>.Fail(
                    "Failed to publish PDFs. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
            finally
            {
                // Restore BACKGROUNDPLOT and clean up the temporary publish copy.
                try { if (previousBgPlot != null) AcApp.SetSystemVariable("BACKGROUNDPLOT", previousBgPlot); } catch { }
                try { if (tempDwg != null && File.Exists(tempDwg)) File.Delete(tempDwg); } catch { }
            }
        }

        /// <summary>
        /// Returns a path to a DWG on disk reflecting the current drawing state. If the active document
        /// is unsaved, it is written to a temporary file in the output folder (whose path is returned via
        /// <paramref name="tempDwg"/> for later cleanup).
        /// </summary>
        private string EnsureSavedToDisk(Document doc, string outputFolder, out string tempDwg)
        {
            tempDwg = null;
            string current = doc.Database.Filename;
            if (!string.IsNullOrEmpty(current) &&
                current.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(current))
            {
                return current;
            }

            // Use a local (out params cannot be captured by a lambda), then assign the out param.
            string temp = Path.Combine(outputFolder, "_publish_source.dwg");
            TransactionHelper.InDocumentLock(doc, () =>
            {
                doc.Database.SaveAs(temp, DwgVersion.Current);
            });
            tempDwg = temp;
            _logger.Debug("Saved a temporary publish copy: " + temp, Category);
            return temp;
        }

        /// <summary>Builds the destination PDF path from the pattern, expanding {drawing} and {date}.</summary>
        private static string ResolveDestination(string outputFolder, string outputFileName, Document doc)
        {
            string drawingName = Path.GetFileNameWithoutExtension(doc.Database.Filename);
            if (string.IsNullOrWhiteSpace(drawingName)) drawingName = "drawing";

            string fileName = Utilities.Text.TokenReplacer.ReplaceDrawingAndDate(
                string.IsNullOrWhiteSpace(outputFileName) ? "{drawing}_sheets.pdf" : outputFileName,
                drawingName, DateTime.Now);

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName += ".pdf";
            return Path.Combine(outputFolder, fileName);
        }

        private static IEnumerable<string> SafeEnumeratePdfs(string folder)
        {
            try { return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf") : Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }
}
