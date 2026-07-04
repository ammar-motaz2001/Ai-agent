using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Provides the corridor assembly (typical cross-section) for step 11.
    /// </summary>
    /// <remarks>
    /// The Civil 3D managed API cannot instantiate stock catalog subassemblies (lanes, shoulders,
    /// daylight) directly, so the recommended and most reliable approach is to build the typical
    /// section once in the drawing template and reference it by name. This service therefore
    /// <b>resolves an existing assembly by name first</b>, and only creates an empty placeholder
    /// assembly (with a prominent warning) when none is found.
    /// </remarks>
    public interface IAssemblyService
    {
        /// <summary>
        /// Returns the handle of an assembly named <see cref="AssemblySettings.Name"/>. If it already
        /// exists (e.g. supplied by the template) it is reused; otherwise an empty assembly is created
        /// and a warning is attached explaining that subassemblies must be added for the corridor to
        /// generate geometry.
        /// </summary>
        OperationResult<string> GetOrCreateAssembly(Document targetDocument, AssemblySettings settings);
    }
}
