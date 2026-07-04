using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="ISaveService"/>. Persists the active document with <c>Database.SaveAs</c> at
    /// the current DWG version.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        private const string Category = "Save";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public SaveService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<string> SaveDrawing(Document targetDocument, string outputFolder, string fileName)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            if (string.IsNullOrWhiteSpace(outputFolder))
                return OperationResult<string>.Fail("No output folder was provided.");

            try
            {
                Directory.CreateDirectory(outputFolder);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "Civil3D-Output.dwg";
                if (!fileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) fileName += ".dwg";

                string path = Path.Combine(outputFolder, fileName);

                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    targetDocument.Database.SaveAs(path, DwgVersion.Current);
                    _logger.Info("Saved final drawing: " + path, Category);
                    return OperationResult<string>.Ok(path, "Drawing saved.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to save the drawing. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }
    }
}
