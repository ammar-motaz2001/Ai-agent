using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Utilities.Text;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IAlignmentService"/>. Wraps <c>Alignment.CreateFromPolyline</c>, resolving
    /// styles gracefully and guaranteeing a unique alignment name.
    /// </summary>
    public sealed class AlignmentService : IAlignmentService
    {
        private const string Category = "Alignment";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public AlignmentService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<string> CreateFromPolyline(Document targetDocument, string polylineHandle, AlignmentSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new AlignmentSettings();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, polylineHandle, out var polylineId))
                        return OperationResult<string>.Fail("Could not find the pasted road polyline to build the alignment from.");

                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // Resolve styles with graceful fallback (missing template styles must not abort the run).
                    ObjectId styleId = StyleResolver.Resolve(
                        civilDoc.Styles.AlignmentStyles, settings.StyleName, _logger, "alignment style");
                    ObjectId labelSetId = StyleResolver.Resolve(
                        civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles, settings.LabelSetName, _logger, "alignment label set");

                    // Ensure a unique name.
                    var existingNames = GetExistingAlignmentNames(civilDoc, db);
                    string name = NameUtils.MakeUnique(settings.Name, existingNames);

                    // siteId = ObjectId.Null keeps the alignment "siteless" (recommended for corridor
                    // design so it does not interact with parcels/other siteless geometry).
                    // Args: name, site, polyline, style, labelSet, eraseExistingEntities, addCurvesBetweenTangents.
                    ObjectId alignmentId = Alignment.CreateFromPolyline(
                        name,
                        ObjectId.Null,
                        polylineId,
                        styleId,
                        labelSetId,
                        false,
                        true);

                    string handle = null;
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var alignment = (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead);
                        handle = alignment.Handle.ToString();
                        _logger.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "Created alignment '{0}' (length {1:F1} m, station {2:F0}+00 to {3:F0}).",
                            alignment.Name, alignment.Length, alignment.StartingStation, alignment.EndingStation),
                            Category);
                    });

                    return OperationResult<string>.Ok(handle, "Alignment created.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the alignment. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Returns the names of all alignments already in the drawing.</summary>
        private static IEnumerable<string> GetExistingAlignmentNames(CivilDocument civilDoc, Database db)
        {
            var names = new List<string>();
            var ids = civilDoc.GetAlignmentIds();
            if (ids == null || ids.Count == 0) return names;

            TransactionHelper.InTransaction(db, tr =>
            {
                foreach (ObjectId id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Alignment a)
                        names.Add(a.Name);
                }
            });
            return names;
        }
    }
}
