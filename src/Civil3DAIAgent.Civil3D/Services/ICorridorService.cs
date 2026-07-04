using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Builds the corridor (step 12) and extracts its Top and Datum surfaces (steps 13-14).
    /// </summary>
    public interface ICorridorService
    {
        /// <summary>
        /// Creates a corridor from the alignment, design profile, and assembly, adds a single baseline
        /// and full-length region, and rebuilds it. Returns the corridor handle.
        /// </summary>
        OperationResult<string> CreateCorridor(
            Document targetDocument, string alignmentHandle, string designProfileHandle,
            string assemblyHandle, CorridorSettings settings);

        /// <summary>
        /// Adds a corridor Top surface (built from links coded "Top") and rebuilds. Returns the
        /// corridor-surface <b>name</b> (corridor surfaces are not standalone entities, so downstream
        /// steps reference them by name on the corridor).
        /// </summary>
        OperationResult<string> CreateTopSurface(Document targetDocument, string corridorHandle, string surfaceName);

        /// <summary>
        /// Adds a corridor Datum surface (built from links coded "Datum") and rebuilds. Returns the
        /// corridor-surface name.
        /// </summary>
        OperationResult<string> CreateDatumSurface(Document targetDocument, string corridorHandle, string surfaceName);
    }
}
