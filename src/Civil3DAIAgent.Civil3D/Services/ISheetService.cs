using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Generates paper-space layout sheets (step 20) and a sheet-set / publish descriptor (step 21).
    /// </summary>
    public interface ISheetService
    {
        /// <summary>
        /// Creates paper-space layouts (Plan, Profile, Sections) each containing a viewport framed to
        /// the relevant content, ready for plotting. Returns the created layout names in plot order.
        /// </summary>
        OperationResult<IReadOnlyList<string>> GenerateLayouts(
            Document targetDocument,
            SheetSettings settings,
            string planFramingHandle,
            IReadOnlyList<string> profileViewHandles,
            IReadOnlyList<string> sectionViewHandles);

        /// <summary>
        /// Writes a sheet-set / batch-publish descriptor (.dsd) into the output folder that references
        /// the given layouts of the current drawing. This is the automatable "sheet set" artefact used
        /// for publishing; a full Sheet Set Manager .dst can be generated from it if desired. Returns
        /// the descriptor file path.
        /// </summary>
        OperationResult<string> CreateSheetSet(
            Document targetDocument, IReadOnlyList<string> layoutNames, string outputFolder, string sheetSetName);
    }
}
