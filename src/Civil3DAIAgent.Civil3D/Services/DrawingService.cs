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
                    var ms = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(sourceDb), OpenMode.ForRead);

                    // 1) Honour an explicitly chosen handle when valid.
                    if (!string.IsNullOrWhiteSpace(preferredHandle) &&
                        TryResolveHandle(sourceDb, preferredHandle, out var chosenId) &&
                        tr.GetObject(chosenId, OpenMode.ForRead) is Curve chosenCurve)
                    {
                        _logger.Info($"Using the pre-selected road centreline on layer '{chosenCurve.Layer}' " +
                                     $"(handle {preferredHandle}).", Category);
                        return OperationResult<string>.Ok(preferredHandle);
                    }

                    // 2) Auto-detect by LAYER. Considers every curve type (Line, Arc, Polyline, ...), so a
                    //    centreline drawn as many separate segments is detected by the layer they share.
                    var roadLayers = SplitCsv(roadLayersCsv);
                    var totalLen = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    var count = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var longest = new Dictionary<string, Curve>(StringComparer.OrdinalIgnoreCase);
                    var longestLen = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                    foreach (ObjectId id in ms)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Curve curve)) continue;
                        double len = SafeCurveLength(curve);
                        if (len <= 0) continue;
                        string layer = curve.Layer ?? "";

                        totalLen.TryGetValue(layer, out var t); totalLen[layer] = t + len;
                        count.TryGetValue(layer, out var c); count[layer] = c + 1;
                        longestLen.TryGetValue(layer, out var ll);
                        if (len > ll) { longestLen[layer] = len; longest[layer] = curve; }
                    }

                    if (totalLen.Count == 0)
                        return OperationResult<string>.Fail(
                            "No line/arc/polyline geometry was found in the source drawing to use as the road centreline.");

                    // Prefer configured road layers (large bias), else the layer with the most curve length.
                    string bestLayer = null; double bestScore = -1;
                    foreach (var kv in totalLen)
                    {
                        bool isRoad = roadLayers.Any(l => string.Equals(l, kv.Key, StringComparison.OrdinalIgnoreCase));
                        double score = kv.Value + (isRoad ? 1e9 : 0);
                        if (score > bestScore) { bestScore = score; bestLayer = kv.Key; }
                    }

                    bool onRoadLayer = roadLayers.Any(l => string.Equals(l, bestLayer, StringComparison.OrdinalIgnoreCase));
                    if (!onRoadLayer)
                        _logger.Warn($"No geometry on the configured road layers ({roadLayersCsv}); using layer " +
                                     $"'{bestLayer}' (most curve length). Set Extraction.RoadPolylineLayers in " +
                                     "appsettings.json to your centreline layer for reliable detection.", Category);

                    var representative = longest[bestLayer];
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Selected road layer '{0}': {1} segment(s), total length {2:F1} m (representative handle {3}).",
                        bestLayer, count[bestLayer], totalLen[bestLayer], representative.Handle), Category);

                    return OperationResult<string>.Ok(representative.Handle.ToString());
                });
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Fail("Failed while selecting the road centreline. " + Explain(ex), ex);
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
            if (!TryResolveHandle(sourceDb, polylineHandle, out var repId))
                return OperationResult<PolylineData>.Fail("Could not resolve the road centreline handle: " + polylineHandle);

            var warnings = new List<string>();
            try
            {
                return TransactionHelper.InTransaction(sourceDb, tr =>
                {
                    if (!(tr.GetObject(repId, OpenMode.ForRead) is Curve repCurve))
                        return OperationResult<PolylineData>.Fail("The selected object is not a curve.");
                    string layer = repCurve.Layer ?? "";

                    // Collect an ordered point list for EVERY curve on the road layer (handles a
                    // centreline split into many separate segments).
                    var pieces = new List<List<Point3d>>();
                    var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(sourceDb), OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForRead) is Curve curve)) continue;
                        if (!string.Equals(curve.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
                        if (SafeCurveLength(curve) <= 0) continue;
                        var pts = GetCurvePoints(curve);
                        if (pts.Count >= 2) pieces.Add(pts);
                    }

                    if (pieces.Count == 0)
                        return OperationResult<PolylineData>.Fail("No usable centreline curves found on layer '" + layer + "'.");

                    // Chain the segments end-to-end (nearest-endpoint greedy) into one point sequence.
                    _logger.Debug($"Assembling centreline from {pieces.Count} segment(s) on layer '{layer}'.", Category);
                    var chained = ChainPieces(pieces, out double maxGap);
                    if (pieces.Count > 1)
                        _logger.Info(string.Format(CultureInfo.InvariantCulture,
                            "Joined {0} centreline segment(s) into one polyline (largest join gap {1:F2} m).", pieces.Count, maxGap), Category);
                    if (maxGap > 5.0)
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "Some centreline segments were up to {0:F1} m apart and were bridged with a straight line. " +
                            "Verify the source drawing if the alignment looks wrong.", maxGap));

                    double totalLength = PolylineLength(chained);
                    if (totalLength <= 0)
                        return OperationResult<PolylineData>.Fail("The assembled centreline has zero length.");

                    List<Point3d> segment;
                    if (totalLength <= lengthMeters + 1e-6)
                    {
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "The road centreline is only {0:F1} m long (shorter than the requested {1:F0} m); the whole " +
                            "centreline was used.", totalLength, lengthMeters));
                        segment = chained;
                    }
                    else
                    {
                        segment = TruncateToLength(chained, lengthMeters);
                    }

                    var data = new PolylineData { Elevation = 0.0, Closed = false, Length = PolylineLength(segment) };
                    foreach (var p in segment) data.Vertices.Add(new PolylineVertex(p.X, p.Y, 0.0));

                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Extracted first {0:F0} m of the road ({1} vertices from {2} segment(s)).",
                        Math.Min(lengthMeters, totalLength), data.VertexCount, pieces.Count), Category);

                    return OperationResult<PolylineData>.Ok(data,
                        $"Extracted {Math.Min(lengthMeters, totalLength):F0} m.", warnings);
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

        /// <summary>Vertex sampling density (metres) when tessellating a curve into points.</summary>
        private const double SampleStepMeters = 2.0;

        /// <summary>Curve length via the start/end parameters; returns 0 on any failure.</summary>
        private static double SafeCurveLength(Curve curve)
        {
            try { return curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam); }
            catch { return 0; }
        }

        /// <summary>
        /// Samples a curve into an ordered list of planar points (Z dropped). Uniform distance sampling
        /// preserves arcs/curves; straight segments contribute their endpoints. Explicit start/end points
        /// are included so chaining connects exactly.
        /// </summary>
        private static List<Point3d> GetCurvePoints(Curve curve)
        {
            var pts = new List<Point3d>();
            double len = SafeCurveLength(curve);
            if (len <= 0) return pts;

            int samples = Math.Max(1, (int)Math.Ceiling(len / SampleStepMeters));
            for (int i = 0; i <= samples; i++)
            {
                double d = len * i / samples;
                try
                {
                    var p = curve.GetPointAtDist(d);
                    pts.Add(new Point3d(p.X, p.Y, 0.0));
                }
                catch { /* skip an unsampleable station */ }
            }
            return pts;
        }

        /// <summary>
        /// Chains ordered point pieces end-to-end into a single connected sequence using a greedy
        /// nearest-endpoint walk, reversing pieces as needed. Reports the largest bridged gap so the
        /// caller can warn about disconnected centrelines.
        /// </summary>
        private static List<Point3d> ChainPieces(List<List<Point3d>> pieces, out double maxGap)
        {
            maxGap = 0.0;
            var chain = new List<Point3d>();
            var remaining = new List<List<Point3d>>(pieces);
            if (remaining.Count == 0) return chain;

            // Deterministic start: the piece endpoint with the smallest (X, then Y).
            int startIdx = 0; bool startReversed = false; double bx = double.MaxValue, by = double.MaxValue;
            for (int i = 0; i < remaining.Count; i++)
            {
                var s = remaining[i][0];
                var e = remaining[i][remaining[i].Count - 1];
                if (s.X < bx || (s.X == bx && s.Y < by)) { bx = s.X; by = s.Y; startIdx = i; startReversed = false; }
                if (e.X < bx || (e.X == bx && e.Y < by)) { bx = e.X; by = e.Y; startIdx = i; startReversed = true; }
            }
            var first = remaining[startIdx];
            if (startReversed) first.Reverse();
            chain.AddRange(first);
            remaining.RemoveAt(startIdx);

            while (remaining.Count > 0)
            {
                var tail = chain[chain.Count - 1];
                int bestI = 0; bool reverse = false; double best = double.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                {
                    double ds = tail.DistanceTo(remaining[i][0]);
                    double de = tail.DistanceTo(remaining[i][remaining[i].Count - 1]);
                    if (ds < best) { best = ds; bestI = i; reverse = false; }
                    if (de < best) { best = de; bestI = i; reverse = true; }
                }

                var piece = remaining[bestI];
                if (reverse) piece.Reverse();
                if (best > maxGap) maxGap = best;

                int startAt = best < 1e-6 ? 1 : 0; // skip a coincident joint point
                for (int k = startAt; k < piece.Count; k++) chain.Add(piece[k]);
                remaining.RemoveAt(bestI);
            }
            return chain;
        }

        /// <summary>Total planar length of an ordered point list.</summary>
        private static double PolylineLength(List<Point3d> pts)
        {
            double len = 0.0;
            for (int i = 1; i < pts.Count; i++) len += pts[i - 1].DistanceTo(pts[i]);
            return len;
        }

        /// <summary>Returns the leading portion of a point list up to <paramref name="maxLen"/> metres.</summary>
        private static List<Point3d> TruncateToLength(List<Point3d> pts, double maxLen)
        {
            var result = new List<Point3d>();
            if (pts.Count == 0) return result;

            result.Add(pts[0]);
            double acc = 0.0;
            for (int i = 1; i < pts.Count; i++)
            {
                double seg = pts[i - 1].DistanceTo(pts[i]);
                if (acc + seg >= maxLen)
                {
                    double t = seg > 1e-9 ? (maxLen - acc) / seg : 0.0;
                    result.Add(new Point3d(
                        pts[i - 1].X + (pts[i].X - pts[i - 1].X) * t,
                        pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * t,
                        0.0));
                    return result;
                }
                acc += seg;
                result.Add(pts[i]);
            }
            return result;
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
