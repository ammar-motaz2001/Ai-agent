using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Utilities.Text;
// Disambiguate: Autodesk.Civil.DatabaseServices.Surface vs Autodesk.AutoCAD.DatabaseServices.Surface.
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
// Disambiguate: Autodesk.Civil.DatabaseServices.Entity vs Autodesk.AutoCAD.DatabaseServices.Entity.
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="ISurfaceService"/>. Builds an existing-ground TIN surface from copied
    /// contours and/or Excel survey points. Every data-addition step is individually guarded so a
    /// problem with one data source (e.g. no contours found) degrades to a warning instead of aborting.
    /// </summary>
    public sealed class SurfaceService : ISurfaceService
    {
        private const string Category = "Surface";

        // Contour weeding/supplementing factors (sensible defaults; angle is in radians ≈ 4°).
        private const double WeedDistance = 15.0;
        private const double WeedAngleRad = 0.069813; // 4 degrees
        private const double SupplementDistance = 100.0;
        private const double SupplementMidOrdinate = 1.0;

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public SurfaceService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        /// <inheritdoc />
        public OperationResult<string> CreateExistingGround(
            Document targetDocument,
            IReadOnlyList<SurveyPoint> points,
            SurfaceSettings settings,
            string contourLayersCsv)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            settings = settings ?? new SurfaceSettings();

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // Unique surface name.
                    var existingNames = GetSurfaceNames(civilDoc, db);
                    string name = NameUtils.MakeUnique(settings.ExistingGroundName, existingNames);

                    // Create the empty TIN surface, then apply the style.
                    ObjectId surfaceId = TinSurface.Create(db, name);
                    ObjectId styleId = StyleResolver.Resolve(civilDoc.Styles.SurfaceStyles, settings.StyleName, _logger, "surface style");

                    string handle = null;
                    int dataSources = 0;

                    TransactionHelper.InTransaction(db, tr =>
                    {
                        var surface = (TinSurface)tr.GetObject(surfaceId, OpenMode.ForWrite);
                        if (!styleId.IsNull) surface.StyleId = styleId;
                        handle = surface.Handle.ToString();

                        // ---- Contour data ----
                        if (settings.BuildFromContours)
                        {
                            var contourIds = CollectContourIds(db, tr, contourLayersCsv);
                            if (contourIds.Count > 0)
                            {
                                // [VERSION] Late-bound: AddContourData arg list varies by release (logged on miss).
                                bool ok = CivilApi.TryInvoke(surface.ContoursDefinition, "AddContourData",
                                    new object[] { contourIds, WeedDistance, WeedAngleRad, SupplementDistance, SupplementMidOrdinate },
                                    _logger);
                                if (ok)
                                {
                                    _logger.Info($"Added {contourIds.Count} contour(s) to the EG surface.", Category);
                                    dataSources++;
                                }
                                else
                                {
                                    warnings.Add("Contour data could not be added (AddContourData signature mismatch — see the log).");
                                }
                            }
                            else
                            {
                                warnings.Add("No contour entities were found to add to the existing-ground surface.");
                            }
                        }
                    });

                    // ---- Survey-point data (COGO points + point group) ----
                    if (settings.BuildFromExcelPoints && points != null && points.Count > 0)
                    {
                        var pointResult = AddSurveyPoints(db, civilDoc, surfaceId, points, name);
                        if (pointResult.Succeeded)
                        {
                            dataSources++;
                            _logger.Info($"Added {points.Count} survey point(s) to the EG surface.", Category);
                        }
                        else
                        {
                            warnings.Add(pointResult.Message);
                        }
                    }

                    if (dataSources == 0)
                        warnings.Add("The existing-ground surface was created but has no data (no contours or points). " +
                                     "Profiles sampled from it will be flat/empty.");

                    _logger.Info($"Existing-ground surface '{name}' created.", Category);
                    return OperationResult<string>.Ok(handle, "EG surface created.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail(
                    "Failed to create the existing-ground surface. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        /// <summary>Collects model-space entity ids whose layer matches a configured contour layer.</summary>
        private static ObjectIdCollection CollectContourIds(Database db, Transaction tr, string contourLayersCsv)
        {
            var result = new ObjectIdCollection();
            var layers = SplitCsv(contourLayersCsv);
            if (layers.Count == 0) return result;

            var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is AcEntity ent &&
                    (ent is Polyline || ent is Polyline2d || ent is Polyline3d || ent is Line) &&
                    layers.Any(l => string.Equals(l, ent.Layer, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        /// <summary>
        /// Creates COGO points for the survey data, gathers them into a point group, and adds that
        /// group to the surface as point data.
        /// </summary>
        private OperationResult AddSurveyPoints(Database db, CivilDocument civilDoc, ObjectId surfaceId,
            IReadOnlyList<SurveyPoint> points, string surfaceName)
        {
            try
            {
                var createdNumbers = new List<uint>();

                // 1) Create COGO points.
                var cogoPoints = civilDoc.CogoPoints;
                TransactionHelper.InTransaction(db, tr =>
                {
                    foreach (var p in points)
                    {
                        if (!p.IsValid) continue;
                        var loc = new Point3d(p.Easting, p.Northing, p.Elevation);
                        ObjectId cpId = cogoPoints.Add(loc, true);
                        if (tr.GetObject(cpId, OpenMode.ForWrite) is CogoPoint cp)
                        {
                            if (!string.IsNullOrEmpty(p.Description))
                                cp.RawDescription = p.Description;
                            createdNumbers.Add(cp.PointNumber);
                        }
                    }
                });

                if (createdNumbers.Count == 0)
                    return OperationResult.Fail("None of the survey points were valid; none were added.");

                // 2) Point group whose query includes exactly the points we created.
                string groupName = NameUtils.MakeUnique(surfaceName + "-Points", GetPointGroupNames(civilDoc, db));
                ObjectId groupId = civilDoc.PointGroups.Add(groupName);

                TransactionHelper.InTransaction(db, tr =>
                {
                    var group = (PointGroup)tr.GetObject(groupId, OpenMode.ForWrite);
                    var query = new StandardPointGroupQuery
                    {
                        IncludeNumbers = string.Join(",", createdNumbers)
                    };
                    group.SetQuery(query);
                });

                // 3) Add the group to the surface.
                TransactionHelper.InTransaction(db, tr =>
                {
                    var surface = (TinSurface)tr.GetObject(surfaceId, OpenMode.ForWrite);
                    surface.PointGroupsDefinition.AddPointGroup(groupId);
                });

                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    "Could not add survey points to the surface. " + (_explainer?.Explain(ex) ?? ex.Message), ex);
            }
        }

        private static IEnumerable<string> GetSurfaceNames(CivilDocument civilDoc, Database db)
        {
            var names = new List<string>();
            var ids = civilDoc.GetSurfaceIds();
            if (ids == null || ids.Count == 0) return names;
            TransactionHelper.InTransaction(db, tr =>
            {
                foreach (ObjectId id in ids)
                    if (tr.GetObject(id, OpenMode.ForRead) is CivSurface s) names.Add(s.Name);
            });
            return names;
        }

        private static IEnumerable<string> GetPointGroupNames(CivilDocument civilDoc, Database db)
        {
            var names = new List<string>();
            var pgs = civilDoc.PointGroups;
            if (pgs == null || pgs.Count == 0) return names;
            TransactionHelper.InTransaction(db, tr =>
            {
                foreach (ObjectId id in pgs)
                    if (tr.GetObject(id, OpenMode.ForRead) is PointGroup pg) names.Add(pg.Name);
            });
            return names;
        }

        private static List<string> SplitCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }
    }
}
