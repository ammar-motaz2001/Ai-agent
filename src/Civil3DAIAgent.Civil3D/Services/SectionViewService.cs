using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="ISectionViewService"/>. Creates one section view per sample line, arranged on
    /// a grid so they don't overlap. Grid cell size derives from the swath width and section scale.
    /// </summary>
    public sealed class SectionViewService : ISectionViewService
    {
        private const string Category = "SectionView";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public SectionViewService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<IReadOnlyList<string>> CreateSectionViews(
            Document targetDocument, string sampleLineGroupHandle, SheetSettings settings)
        {
            if (targetDocument == null)
                return OperationResult<IReadOnlyList<string>>.Fail("The target drawing is not available.");
            settings = settings ?? new SheetSettings();

            var warnings = new List<string>();
            var handles = new List<string>();

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    if (!HandleUtils.TryResolve(db, sampleLineGroupHandle, out var groupId))
                        return OperationResult<IReadOnlyList<string>>.Fail("Could not find the sample-line group for section views.");

                    // Gather sample line ids.
                    var sampleLineIds = new List<ObjectId>();
                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead);
                        foreach (ObjectId id in group.GetSampleLineIds())
                            sampleLineIds.Add(id);
                    });

                    if (sampleLineIds.Count == 0)
                        return OperationResult<IReadOnlyList<string>>.Fail("The sample-line group has no sample lines.");

                    // Grid layout parameters.
                    int columns = Math.Max(1, settings.SectionsPerSheet);
                    Point3d origin = BesideExtents(db);
                    double cellW = ComputeCellWidth(settings);
                    double cellH = ComputeCellHeight(settings);

                    int index = 0;
                    foreach (var slId in sampleLineIds)
                    {
                        int row = index / columns;
                        int col = index % columns;
                        var insertion = new Point3d(origin.X + col * cellW, origin.Y - row * cellH, 0.0);

                        try
                        {
                            // [VERSION] Create the section view at the computed grid location.
                            ObjectId svId = SectionView.Create(slId, insertion);
                            TransactionHelper.InTransaction(db, tr =>
                            {
                                var sv = (SectionView)tr.GetObject(svId, OpenMode.ForRead);
                                handles.Add(sv.Handle.ToString());
                            });
                        }
                        catch (Exception ex)
                        {
                            warnings.Add($"Section view #{index + 1} could not be created: " +
                                         (_explainer?.Explain(ex) ?? ex.Message));
                        }
                        index++;
                    }

                    _logger.Info($"Created {handles.Count} section view(s).", Category);
                    return OperationResult<IReadOnlyList<string>>.Ok(handles, $"{handles.Count} section views created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<string>>.Fail(
                    "Failed to create section views. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Cell width = full swath plus a gutter, scaled down by the section scale.</summary>
        private static double ComputeCellWidth(SheetSettings settings)
        {
            double swath = 50.0; // conservative default full-swath (m); overridden by actual view size at plot.
            double scale = settings.SectionScale > 0 ? settings.SectionScale : 100.0;
            return Math.Max(30.0, swath + scale * 0.02) + 20.0;
        }

        /// <summary>Cell height = typical section height plus a gutter.</summary>
        private static double ComputeCellHeight(SheetSettings settings)
        {
            double scale = settings.SectionScale > 0 ? settings.SectionScale : 100.0;
            return Math.Max(20.0, scale * 0.15) + 15.0;
        }

        /// <summary>A point to the right of the drawing extents where the section grid begins.</summary>
        private static Point3d BesideExtents(Database db)
        {
            try
            {
                var min = db.Extmin;
                var max = db.Extmax;
                return new Point3d(max.X + 100.0, max.Y, 0.0);
            }
            catch
            {
                return new Point3d(500, 0, 0);
            }
        }
    }
}
