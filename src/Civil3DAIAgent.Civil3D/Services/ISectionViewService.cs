using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>Creates section views for each sample line in a group (step 18).</summary>
    public interface ISectionViewService
    {
        /// <summary>
        /// Creates a section view for every sample line in the group, laid out on a grid whose column
        /// count derives from <see cref="SheetSettings.SectionsPerSheet"/>. Returns the created section
        /// view handles.
        /// </summary>
        OperationResult<IReadOnlyList<string>> CreateSectionViews(
            Document targetDocument, string sampleLineGroupHandle, SheetSettings settings);
    }
}
