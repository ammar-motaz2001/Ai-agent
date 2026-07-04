using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Creates Civil 3D profiles: the existing-ground profile sampled from a surface (step 9) and the
    /// design profile created by layout (step 10).
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Creates an existing-ground profile by projecting the surface onto the alignment. Returns the
        /// profile handle.
        /// </summary>
        OperationResult<string> CreateExistingGroundProfile(
            Document targetDocument, string alignmentHandle, string surfaceHandle, ProfileSettings settings);

        /// <summary>
        /// Creates a design profile "by layout". When
        /// <see cref="ProfileSettings.AutoGenerateFromExistingGround"/> is enabled, PVIs are sampled
        /// from the surface along the alignment (offset by the configured vertical shift) to produce a
        /// buildable design profile; otherwise an empty profile is created for later manual layout.
        /// Returns the profile handle.
        /// </summary>
        OperationResult<string> CreateDesignProfile(
            Document targetDocument, string alignmentHandle, string surfaceHandle, ProfileSettings settings);
    }
}
