using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Geometry;
using Civil3DAIAgent.Models.Results;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Default <see cref="IDrawingService"/> implementation. Covers workflow steps 1-6 using the
    /// AutoCAD .NET database API. Every public method is wrapped so that expected problems become
    /// failed results and unexpected exceptions are explained via <see cref="IExceptionExplainer"/>
    /// rather than crashing the host.
    /// </summary>
    public sealed class DrawingService : IDrawingService
    {
        private const string Category = "Drawing";

        private readonly ILogger _logger;
        private readonly IExceptionExplainer _explainer;

        /// <summary>Creates the service.</summary>
        public DrawingService(ILogger logger, IExceptionExplainer explainer)
        {
            _logger = logger ?? NullLogger.Instance;
            _explainer = explainer;
        }

        // ------------------------------------------------------------------ Step 1: Open source
        /// <inheritdoc />
        public OperationResult<Database> OpenSourceDatabase(string dwgPath)
        {
            if (string.IsNullOrWhiteSpace(dwgPath) || !File.Exists(dwgPath))
                return OperationResult<Database>.Fail("The source DWG was not found: " + dwgPath);

            Database db = null;
            try
            {
                // A "side database" lets us read the source without opening it as the active document.
                _logger.Debug("API: new Database(false, true)", Category);
                db = new Database(false, true);
                _logger.Debug("API: Database.ReadDwgFile('" + dwgPath + "', FileShare.Read)", Category);
                db.ReadDwgFile(dwgPath, FileShare.Read, allowCPConversion: true, password: null);
                _logger.Debug("API: Database.CloseInput(true)", Category);
                db.CloseInput(true);

                _logger.Info("Opened source drawing: " + Path.GetFileName(dwgPath), Category);
                return OperationResult<Database>.Ok(db);
            }
            catch (Exception ex)
            {
                db?.Dispose();
                return OperationResult<Database>.Fail(
                    "Could not open the source drawing. " + Explain(ex), ex);
            }
        }

        // ------------------------------------------------------------------ Step 2: Select polyline
        /// <inheritdoc />
        public OperationResult<string> SelectRoadPolyline(Database sourceDb, string preferredHandle, string roadLayersCsv)
        {
            if (sourceDb == null)
                return OperationResult<string>.Fail("The source database is not open.");

            try
            {
                return TransactionHelper.InTransaction(sourceDb, tr =>
                {
                    // 1) Honour an explicitly chosen handle when valid.
                    if (!string.IsNullOrWhiteSpace(preferredHandle) &&
                        TryResolveHandle(sourceDb, preferredHandle, out var chosenId) &&
                        tr.GetObject(chosenId, OpenMode.ForRead) is Curve)
                    {
                        _logger.Info("Using the pre-selected road polyline (handle " + preferredHandle + ").", Category);
                        return OperationResult<string>.Ok(preferredHandle);
                    }

                    // 2) Auto-detect: longest polyline on a configured road layer.
                    var roadLayers = SplitCsv(roadLayersCsv);
                    var ms = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(sourceDb), OpenMode.ForRead);

                    Curve bestOnLayer = null, bestAny = null;
                    double bestOnLayerLen = 0, bestAnyLen = 0;

                    foreach (ObjectId id in ms)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Curve curve)) continue;
                        if (!IsPolylineType(curve)) continue;

                        double len = SafeCurveLength(curve);
                        if (len <= 0) continue;

                        if (len > bestAnyLen) { bestAnyLen = len; bestAny = curve; }

                        if (roadLayers.Count > 0 &&
                            roadLayers.Any(l => string.Equals(l, curve.Layer, StringComparison.OrdinalIgnoreCase)) &&
                            len > bestOnLayerLen)
                        {
                            bestOnLayerLen = len; bestOnLayer = curve;
                        }
                    }

                    var winner = bestOnLayer ?? bestAny;
                    if (winner == null)
                        return OperationResult<string>.Fail(
                            "No polyline was found in the source drawing to use as the road centreline.");

                    if (bestOnLayer == null)
                        _logger.Warn("No polyline found on the configured road layers; using the longest " +
                                     "polyline in the drawing instead.", Category);

                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Selected road polyline on layer '{0}', length {1:F1} m (handle {2}).",
                        winner.Layer, SafeCurveLength(winner), winner.Handle), Category);

                    return OperationResult<string>.Ok(winner.Handle.ToString());
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail("Failed while selecting the road polyline. " + Explain(ex), ex);
            }
        }

        // ------------------------------------------------------------------ Step 3: Extract segment
        /// <inheritdoc />
        public OperationResult<PolylineData> ExtractFirstSegment(Database sourceDb, string polylineHandle, double lengthMeters)
        {
            if (sourceDb == null)
                return OperationResult<PolylineData>.Fail("The source database is not open.");
            if (lengthMeters <= 0)
                return OperationResult<PolylineData>.Fail("The extraction length must be greater than zero.");
            if (!TryResolveHandle(sourceDb, polylineHandle, out var plId))
                return OperationResult<PolylineData>.Fail("Could not resolve the road polyline handle: " + polylineHandle);

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InTransaction(sourceDb, tr =>
                {
                    if (!(tr.GetObject(plId, OpenMode.ForRead) is Curve curve))
                        return OperationResult<PolylineData>.Fail("The selected object is not a curve/polyline.");

                    double totalLength = SafeCurveLength(curve);
                    if (totalLength <= 0)
                        return OperationResult<PolylineData>.Fail("The selected polyline has zero length.");

                    // If shorter than requested, take the whole thing.
                    if (totalLength <= lengthMeters + 1e-6)
                    {
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "The road polyline is only {0:F1} m long (shorter than the requested {1:F0} m); " +
                            "the entire polyline was used.", totalLength, lengthMeters));
                        var wholeData = BuildPolylineData(curve, warnings);
                        return OperationResult<PolylineData>.Ok(wholeData,
                            $"Extracted full polyline ({totalLength:F1} m).", warnings);
                    }

                    // Split at the point that is exactly lengthMeters along the curve.
                    Point3d splitPt = curve.GetPointAtDist(lengthMeters);
                    double splitParam = curve.GetParameterAtPoint(splitPt);

                    var splits = curve.GetSplitCurves(new DoubleCollection(new[] { splitParam }));
                    if (splits == null || splits.Count == 0)
                        return OperationResult<PolylineData>.Fail(
                            "Civil 3D could not split the polyline at the requested station.");

                    // The first split curve is the leading segment we want.
                    var firstSegment = splits[0] as Curve;
                    var data = BuildPolylineData(firstSegment, warnings);

                    // Dispose the temporary split curves (they are non-database-resident clones).
                    foreach (DBObject obj in splits) { try { obj.Dispose(); } catch { /* ignore */ } }

                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Extracted first {0:F0} m of the road ({1} vertices).", lengthMeters, data.VertexCount), Category);

                    return OperationResult<PolylineData>.Ok(data,
                        $"Extracted {lengthMeters:F0} m.", warnings);
                });
            }
            catch (Exception ex)
            {
                return OperationResult<PolylineData>.Fail("Failed while extracting the road segment. " + Explain(ex), ex);
            }
        }

        // ------------------------------------------------------------------ Step 4: New drawing
        /// <inheritdoc />
        public OperationResult<Document> CreateNewDrawing(string templatePath)
        {
            try
            {
                string template = templatePath;
                if (string.IsNullOrWhiteSpace(template) || !File.Exists(template))
                {
                    _logger.Warn("Drawing template not found ('" + templatePath + "'). Creating a default " +
                                 "drawing; Civil 3D styles referenced later may be missing.", Category);
                    template = null; // DocumentManager.Add(null) creates a default drawing.
                }

                // Creating via the document manager makes the new drawing the active document, which is
                // required for all subsequent Civil 3D operations (alignment, surface, ...).
                // NOTE: DocumentManager.Add requires application context (no active command). The UI runs
                // the workflow from a modeless window on the main thread, which satisfies this.
                var docs = AcApp.DocumentManager;
                _logger.Debug("API: DocumentManager.Add('" + (template ?? "(default)") + "')", Category);
                var newDoc = docs.Add(template);
                _logger.Debug("API: set DocumentManager.MdiActiveDocument", Category);
                docs.MdiActiveDocument = newDoc;

                _logger.Info("Created new drawing" +
                             (template != null ? " from template " + Path.GetFileName(template) : " (default)") + ".", Category);
                return OperationResult<Document>.Ok(newDoc);
            }
            catch (Exception ex)
            {
                return OperationResult<Document>.Fail("Could not create the new drawing. " + Explain(ex), ex);
            }
        }

        // ------------------------------------------------------------------ Step 5: Paste polyline
        /// <inheritdoc />
        public OperationResult<string> PastePolyline(Document targetDocument, PolylineData data, string layerName)
        {
            if (targetDocument == null)
                return OperationResult<string>.Fail("The target drawing is not available.");
            if (data == null || data.VertexCount < 2)
                return OperationResult<string>.Fail("There is no valid polyline geometry to paste.");

            try
            {
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var db = targetDocument.Database;
                    return TransactionHelper.InTransaction(db, tr =>
                    {
                        var layerId = EnsureLayer(db, tr, layerName);

                        var pl = new Polyline();
                        pl.SetDatabaseDefaults(db);
                        for (int i = 0; i < data.Vertices.Count; i++)
                        {
                            var v = data.Vertices[i];
                            pl.AddVertexAt(i, new Point2d(v.X, v.Y), v.Bulge, 0.0, 0.0);
                        }
                        pl.Elevation = data.Elevation;
                        pl.Closed = data.Closed;
                        if (!layerId.IsNull) pl.LayerId = layerId;

                        var ms = (BlockTableRecord)tr.GetObject(
                            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                        ms.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);

                        _logger.Info("Pasted road polyline into the new drawing at original coordinates.", Category);
                        return OperationResult<string>.Ok(pl.Handle.ToString());
                    });
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail("Failed while pasting the polyline. " + Explain(ex), ex);
            }
        }

        // ------------------------------------------------------------------ Step 6: Copy contours
        /// <inheritdoc />
        public OperationResult<int> CopyContours(Database sourceDb, Document targetDocument, string contourLayersCsv)
        {
            if (sourceDb == null)
                return OperationResult<int>.Fail("The source database is not open.");
            if (targetDocument == null)
                return OperationResult<int>.Fail("The target drawing is not available.");

            var contourLayers = SplitCsv(contourLayersCsv);
            if (contourLayers.Count == 0)
                return OperationResult<int>.Ok(0, "No contour layers configured; nothing copied.");

            try
            {
                // 1) Collect the source contour entity ids.
                var ids = new ObjectIdCollection();
                TransactionHelper.InTransaction(sourceDb, tr =>
                {
                    var ms = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(sourceDb), OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Entity ent &&
                            contourLayers.Any(l => string.Equals(l, ent.Layer, StringComparison.OrdinalIgnoreCase)))
                        {
                            ids.Add(id);
                        }
                    }
                });

                if (ids.Count == 0)
                    return OperationResult<int>.Ok(0, "No contour entities found on the configured layers.");

                // 2) Clone them across databases, preserving layers and elevations.
                return TransactionHelper.InDocumentLock(targetDocument, () =>
                {
                    var destDb = targetDocument.Database;
                    var destMs = SymbolUtilityServices.GetBlockModelSpaceId(destDb);
                    using (var mapping = new IdMapping())
                    {
                        destDb.WblockCloneObjects(ids, destMs, mapping, DuplicateRecordCloning.Replace, false);
                    }

                    _logger.Info($"Copied {ids.Count} contour entit(ies) into the new drawing.", Category);
                    return OperationResult<int>.Ok(ids.Count, $"Copied {ids.Count} contour entities.");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<int>.Fail("Failed while copying contours. " + Explain(ex), ex);
            }
        }

        // ============================================================ private helpers ============

        /// <summary>Resolves a hex handle string to an <see cref="ObjectId"/> in the given database.</summary>
        private static bool TryResolveHandle(Database db, string handleString, out ObjectId id)
        {
            id = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(handleString)) return false;
            try
            {
                long value = Convert.ToInt64(handleString, 16);
                var handle = new Handle(value);
                id = db.GetObjectId(false, handle, 0);
                return !id.IsNull;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True for the polyline family (lightweight, 2D, 3D).</summary>
        private static bool IsPolylineType(Curve curve)
        {
            return curve is Polyline || curve is Polyline2d || curve is Polyline3d;
        }

        /// <summary>Curve length via the start/end parameters; returns 0 on any failure.</summary>
        private static double SafeCurveLength(Curve curve)
        {
            try { return curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam); }
            catch { return 0; }
        }

        /// <summary>
        /// Builds neutral <see cref="PolylineData"/> from a curve. Lightweight polylines are captured
        /// exactly (vertices + bulges). Other curve types are tessellated into straight segments (with a
        /// warning) so the workflow can still proceed.
        /// </summary>
        private static PolylineData BuildPolylineData(Curve curve, List<string> warnings)
        {
            var data = new PolylineData();

            if (curve is Polyline lwpl)
            {
                data.Elevation = lwpl.Elevation;
                data.Closed = lwpl.Closed;
                int n = lwpl.NumberOfVertices;
                for (int i = 0; i < n; i++)
                {
                    var pt = lwpl.GetPoint2dAt(i);
                    double bulge = lwpl.GetBulgeAt(i);
                    data.Vertices.Add(new PolylineVertex(pt.X, pt.Y, bulge));
                }
                data.Length = SafeCurveLength(lwpl);
                return data;
            }

            // Fallback: sample the curve into straight chords.
            warnings?.Add("The road object was not a lightweight polyline; it was approximated by sampling. " +
                          "For best fidelity, use a 2D polyline as the road centreline.");

            double total = SafeCurveLength(curve);
            int samples = Math.Max(2, (int)Math.Ceiling(total / 5.0)); // a vertex roughly every 5 m
            for (int i = 0; i <= samples; i++)
            {
                double dist = total * i / samples;
                Point3d p;
                try { p = curve.GetPointAtDist(dist); }
                catch { continue; }
                data.Vertices.Add(new PolylineVertex(p.X, p.Y, 0.0));
            }
            data.Elevation = 0.0;
            data.Closed = false;
            data.Length = total;
            return data;
        }

        /// <summary>Ensures a layer exists (creating it if needed) and returns its id (null id on failure).</summary>
        private ObjectId EnsureLayer(Database db, Transaction tr, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return ObjectId.Null;
            try
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(layerName)) return lt[layerName];

                lt.UpgradeOpen();
                var ltr = new LayerTableRecord { Name = layerName };
                var id = lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
                return id;
            }
            catch (Exception ex)
            {
                _logger.Warn("Could not create layer '" + layerName + "'; using the current layer. " + ex.Message, Category);
                return ObjectId.Null;
            }
        }

        /// <summary>Splits a comma-separated list into trimmed, non-empty entries.</summary>
        private static List<string> SplitCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0)
                      .ToList();
        }

        /// <summary>Uses the explainer when available, else the raw message.</summary>
        private string Explain(Exception ex)
        {
            return _explainer != null ? _explainer.Explain(ex) : ex.Message;
        }
    }
}
