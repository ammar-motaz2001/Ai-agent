using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>
    /// Creates the sample-line group and sample lines along the alignment (step 15) that later drive
    /// section views and material computation.
    /// </summary>
    public interface ISampleLineService
    {
        /// <summary>
        /// Creates a sample-line group on the alignment and populates it with sample lines at the
        /// configured interval and swath widths. Best-effort attempts to register the supplied surface
        /// handles as sampled data sources. Returns the sample-line group handle.
        /// </summary>
        OperationResult<string> CreateSampleLines(
            Document targetDocument,
            string alignmentHandle,
            SampleLineSettings settings,
            IReadOnlyList<string> surfaceHandlesToSample,
            string corridorHandle);
    }
}
