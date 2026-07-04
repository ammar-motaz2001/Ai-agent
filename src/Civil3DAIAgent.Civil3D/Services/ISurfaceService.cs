using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Creates and manages Civil 3D TIN surfaces. Covers the existing-ground surface (step 8). The
    /// corridor top/datum surfaces (steps 13-14) are produced by <c>ICorridorService</c> because they
    /// are extracted from the corridor object.
    /// </summary>
    public interface ISurfaceService
    {
        /// <summary>
        /// Creates the existing-ground TIN surface in the target document. When enabled in
        /// <paramref name="settings"/>, contour entities on the given layers are added as contour data
        /// and the supplied <paramref name="points"/> are added as COGO-point data. Returns the surface
        /// handle. Succeeds (with a warning) even if it ends up with no data, so the run can continue.
        /// </summary>
        OperationResult<string> CreateExistingGround(
            Document targetDocument,
            IReadOnlyList<SurveyPoint> points,
            SurfaceSettings settings,
            string contourLayersCsv);
    }
}
