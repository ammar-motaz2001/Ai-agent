using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Utilities.Text;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="ISheetService"/>. Builds paper-space layouts with framed viewports using the
    /// AutoCAD layout API, and writes a .dsd batch-publish descriptor for the sheet set.
    /// </summary>
    /// <remarks>
    /// A full Civil 3D Plan Production run (view-frame groups + match lines) is intentionally not used
    /// here because its API requires template styles that cannot be assumed present. Straightforward
    /// layouts + viewports are robust, plot to PDF cleanly, and work with any template.
    /// </remarks>
    public sealed class SheetService : ISheetService
    {
        private const string Category = "Sheets";

        // A1 landscape paper (mm) with a margin; viewport fills most of it.
        private const double PaperWidth = 841.0;
        private const double PaperHeight = 594.0;
        private const double Margin = 20.0;

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public SheetService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        // ------------------------------------------------------------------ Step 20: Layouts
        /// <inheritdoc />
        public OperationResult<IReadOnlyList<string>> GenerateLayouts(
            Document targetDocument, SheetSettings settings, string planFramingHandle,
            IReadOnlyList<string> profileViewHandles, IReadOnlyList<string> sectionViewHandles)
        {
            if (targetDocument == null)
                return OperationResult<IReadOnlyList<string>>.Fail("The target drawing is not available.");
            settings = settings ?? new SheetSettings();

            var warnings = new List<string>();
            var created = new List<string>();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    var existing = GetLayoutNames(db);

                    // Plan sheet – framed to the alignment/road polyline.
                    var planExt = CombinedExtents(db, Single(planFramingHandle));
                    if (planExt.HasValue)
                        created.Add(CreateLayoutWithViewport(db, NameUtils.MakeUnique("AI-Plan", existing), planExt.Value, warnings));

                    // Profile sheet – framed to the profile view(s).
                    var profExt = CombinedExtents(db, profileViewHandles);
                    if (profExt.HasValue)
                        created.Add(CreateLayoutWithViewport(db, NameUtils.MakeUnique("AI-Profile", existing), profExt.Value, warnings));

                    // Section sheet(s) – framed to the section grid.
                    var sectExt = CombinedExtents(db, sectionViewHandles);
                    if (sectExt.HasValue)
                        created.Add(CreateLayoutWithViewport(db, NameUtils.MakeUnique("AI-Sections", existing), sectExt.Value, warnings));

                    created.RemoveAll(string.IsNullOrEmpty);
                    if (created.Count == 0)
                        warnings.Add("No layouts were generated because no content extents could be determined.");

                    _logger.Info($"Generated {created.Count} layout sheet(s): {string.Join(", ", created)}.", Category);
                    return OperationResult<IReadOnlyList<string>>.Ok(created, $"{created.Count} layouts created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<string>>.Fail(
                    "Failed to generate layout sheets. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Creates one layout containing a single viewport framed to the given model extents.</summary>
        private string CreateLayoutWithViewport(Database db, string layoutName, Extents3d modelExtents, List<string> warnings)
        {
            try
            {
                var lm = LayoutManager.Current;
                ObjectId layoutId = lm.CreateLayout(layoutName);

                TransactionHelper.InTransaction(db, tr =>
                {
                    var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                    var paperSpace = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                    // Model region to show.
                    double mW = Math.Max(1.0, modelExtents.MaxPoint.X - modelExtents.MinPoint.X);
                    double mH = Math.Max(1.0, modelExtents.MaxPoint.Y - modelExtents.MinPoint.Y);
                    var mCenter = new Point2d(
                        (modelExtents.MinPoint.X + modelExtents.MaxPoint.X) / 2.0,
                        (modelExtents.MinPoint.Y + modelExtents.MaxPoint.Y) / 2.0);

                    double vpW = PaperWidth - 2 * Margin;
                    double vpH = PaperHeight - 2 * Margin;

                    var vp = new Viewport();
                    paperSpace.AppendEntity(vp);
                    tr.AddNewlyCreatedDBObject(vp, true);

                    vp.CenterPoint = new Point3d(PaperWidth / 2.0, PaperHeight / 2.0, 0.0);
                    vp.Width = vpW;
                    vp.Height = vpH;
                    vp.ViewCenter = mCenter;

                    // Fit the model extents to the viewport, preserving aspect (add 5% padding).
                    double paperAspect = vpW / vpH;
                    double modelAspect = mW / mH;
                    vp.ViewHeight = (modelAspect > paperAspect ? mW / paperAspect : mH) * 1.05;

                    vp.On = true;
                });

                return layoutName;
            }
            catch (Exception ex)
            {
                warnings.Add($"Layout '{layoutName}' could not be created: " + (_explainer?.Explain(ex) ?? ex.Message));
                return null;
            }
        }

        // ------------------------------------------------------------------ Step 21: Sheet set
        /// <inheritdoc />
        public OperationResult<string> CreateSheetSet(
            Document targetDocument, IReadOnlyList<string> layoutNames, string outputFolder, string sheetSetName)
        {
            if (layoutNames == null || layoutNames.Count == 0)
                return OperationResult<string>.Fail("There are no layouts to include in the sheet set.");
            if (string.IsNullOrWhiteSpace(outputFolder))
                return OperationResult<string>.Fail("No output folder was provided for the sheet set.");

            try
            {
                Directory.CreateDirectory(outputFolder);
                string dwgPath = targetDocument?.Database?.Filename ?? "";
                string dsdPath = Path.Combine(outputFolder, SanitizeFileName(sheetSetName) + ".dsd");

                // Build a DSD (Drawing Set Description) targeting PDF. This is a real, publishable
                // sheet-set descriptor; it can also be opened in the Publish dialog.
                var sb = new StringBuilder();
                sb.AppendLine("[DWF6Version]");
                sb.AppendLine("Ver=1");
                sb.AppendLine("[DWF6MinorVersion]");
                sb.AppendLine("MinorVer=1");
                sb.AppendLine("[Target]");
                sb.AppendLine("Type=6"); // 6 = PDF
                sb.AppendLine("OUT=" + outputFolder);
                sb.AppendLine("PWD=");

                int i = 1;
                foreach (var layout in layoutNames)
                {
                    sb.AppendLine("[DWF6Sheet:" + sheetSetName + "-" + i + "]");
                    sb.AppendLine("DWG=" + dwgPath);
                    sb.AppendLine("Layout=" + layout);
                    sb.AppendLine("Setup=");
                    sb.AppendLine("OriginalSheetPath=" + dwgPath);
                    sb.AppendLine("Has Plot Port=0");
                    sb.AppendLine("Has3DDWF=0");
                    i++;
                }

                File.WriteAllText(dsdPath, sb.ToString(), Encoding.Default);
                _logger.Info($"Wrote sheet-set descriptor with {layoutNames.Count} sheet(s): {dsdPath}", Category);
                return OperationResult<string>.Ok(dsdPath, "Sheet set (DSD) created.");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the sheet set. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        // ============================================================ helpers ============
        private static IReadOnlyList<string> Single(string handle)
            => string.IsNullOrEmpty(handle) ? new List<string>() : new List<string> { handle };

        /// <summary>Combined geometric extents of the entities with the given handles; null if none resolve.</summary>
        private static Extents3d? CombinedExtents(Database db, IReadOnlyList<string> handles)
        {
            if (handles == null || handles.Count == 0) return null;

            Extents3d? combined = null;
            TransactionHelper.InTransaction(db, tr =>
            {
                foreach (var h in handles)
                {
                    if (!HandleUtils.TryResolve(db, h, out var id)) continue;
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    try
                    {
                        var ext = ent.GeometricExtents;
                        if (combined == null) combined = ext;
                        else { var c = combined.Value; c.AddExtents(ext); combined = c; }
                    }
                    catch
                    {
                        // Entity without geometric extents; skip.
                    }
                }
            });
            return combined;
        }

        private static List<string> GetLayoutNames(Database db)
        {
            var names = new List<string>();
            TransactionHelper.InTransaction(db, tr =>
            {
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (var entry in dict)
                    names.Add(entry.Key);
            });
            return names;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "SheetSet";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
