using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>Creates profile views for the alignment (step 19).</summary>
    public interface IProfileViewService
    {
        /// <summary>
        /// Creates a profile view for the alignment (showing its EG and design profiles) at a clear
        /// location in model space. Returns the created profile-view handle(s).
        /// </summary>
        OperationResult<IReadOnlyList<string>> CreateProfileViews(
            Document targetDocument, string alignmentHandle, SheetSettings settings);
    }
}
