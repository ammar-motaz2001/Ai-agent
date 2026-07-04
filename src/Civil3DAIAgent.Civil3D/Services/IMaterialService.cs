using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Volumes;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Computes earthwork quantities on a sample-line group: a material list / quantity takeoff
    /// (step 16) and the extracted cut &amp; fill volumes (step 17).
    /// </summary>
    public interface IMaterialService
    {
        /// <summary>
        /// Adds a material list to the sample-line group comparing the datum surface against the
        /// existing-ground surface using the configured quantity-takeoff criteria. Returns the created
        /// material list's name (used by <see cref="ComputeCutFill"/>).
        /// </summary>
        OperationResult<string> ComputeMaterials(
            Document targetDocument, string sampleLineGroupHandle,
            string existingGroundSurfaceHandle, string datumSurfaceName, MaterialSettings settings);

        /// <summary>
        /// Reads the volumes from the material list produced by <see cref="ComputeMaterials"/> and
        /// returns aggregate cut/fill quantities (adjusted by the cut/fill factors).
        /// </summary>
        OperationResult<VolumeSummary> ComputeCutFill(
            Document targetDocument, string sampleLineGroupHandle, string materialListName, MaterialSettings settings);
    }
}
