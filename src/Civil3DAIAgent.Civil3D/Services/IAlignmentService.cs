using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Creates and manages Civil 3D alignments (workflow step 7).
    /// </summary>
    public interface IAlignmentService
    {
        /// <summary>
        /// Creates an alignment from the polyline identified by <paramref name="polylineHandle"/> in
        /// the target document, applying the configured style and label set. The name is made unique to
        /// avoid duplicate-name errors. Returns the new alignment's handle.
        /// </summary>
        OperationResult<string> CreateFromPolyline(Document targetDocument, string polylineHandle, AlignmentSettings settings);
    }
}
