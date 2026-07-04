using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3DAIAgent.Models.Geometry;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Drawing-level Civil 3D / AutoCAD operations that cover workflow steps 1-6: opening the source
    /// drawing, selecting the road polyline, extracting the first segment, creating the new drawing,
    /// pasting the polyline (preserving coordinates), and copying contours.
    /// </summary>
    /// <remarks>
    /// Methods take primitive inputs and return <see cref="OperationResult{T}"/> so the Application
    /// step classes stay thin and free of try/catch. Implementations must be called on the document
    /// thread (or inside a document lock) because they touch live databases.
    /// </remarks>
    public interface IDrawingService
    {
        /// <summary>
        /// Opens the source DWG as a read-only side <see cref="Database"/> (does not change the active
        /// document). The caller registers the returned database for disposal at run end.
        /// </summary>
        OperationResult<Database> OpenSourceDatabase(string dwgPath);

        /// <summary>
        /// Determines the road centreline polyline in <paramref name="sourceDb"/>. If
        /// <paramref name="preferredHandle"/> is a valid handle it is used; otherwise the longest
        /// polyline on any layer listed in <paramref name="roadLayersCsv"/> is chosen (falling back to
        /// the longest polyline in the drawing). Returns the chosen entity's handle string.
        /// </summary>
        OperationResult<string> SelectRoadPolyline(Database sourceDb, string preferredHandle, string roadLayersCsv);

        /// <summary>
        /// Extracts the first <paramref name="lengthMeters"/> of the polyline identified by
        /// <paramref name="polylineHandle"/> in <paramref name="sourceDb"/> and returns its geometry as
        /// a neutral <see cref="PolylineData"/>. If the polyline is shorter than the requested length,
        /// the whole polyline is returned (with a warning).
        /// </summary>
        OperationResult<PolylineData> ExtractFirstSegment(Database sourceDb, string polylineHandle, double lengthMeters);

        /// <summary>
        /// Creates a new drawing from <paramref name="templatePath"/> (a .dwt) and makes it the active
        /// document. If the template is missing, a default drawing is created (with a warning). Returns
        /// the new <see cref="Document"/>.
        /// </summary>
        OperationResult<Document> CreateNewDrawing(string templatePath);

        /// <summary>
        /// Rebuilds the extracted polyline in the model space of <paramref name="targetDocument"/> at
        /// the exact original coordinates, on the layer <paramref name="layerName"/> (created if
        /// needed). Returns the pasted polyline's handle.
        /// </summary>
        OperationResult<string> PastePolyline(Document targetDocument, PolylineData data, string layerName);

        /// <summary>
        /// Copies every entity on the contour layers listed in <paramref name="contourLayersCsv"/> from
        /// <paramref name="sourceDb"/> into the model space of <paramref name="targetDocument"/>,
        /// preserving layers and 3D elevations. Returns the number of entities copied.
        /// </summary>
        OperationResult<int> CopyContours(Database sourceDb, Document targetDocument, string contourLayersCsv);
    }
}
