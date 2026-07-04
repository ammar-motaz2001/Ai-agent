using System.Collections.Generic;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// Publishes drawing layouts to PDF. The concrete implementation lives in the Civil3D layer
    /// (it uses the AutoCAD Publisher / plotting API), but the contract is neutral so the workflow
    /// engine can request PDFs without knowing about AutoCAD.
    /// </summary>
    public interface IPdfPublisher
    {
        /// <summary>
        /// Publishes the named paper-space layouts of the currently open drawing to PDF.
        /// </summary>
        /// <param name="layoutNames">Layout (tab) names to publish, in order.</param>
        /// <param name="outputFolder">Folder where PDF(s) are written.</param>
        /// <param name="outputFileName">
        /// Desired file name for the merged PDF (used when <paramref name="mergeToSingleFile"/> is true).
        /// </param>
        /// <param name="pageSetupName">Named page setup to apply, or empty for the layout's own setup.</param>
        /// <param name="dpi">Plot resolution in DPI.</param>
        /// <param name="mergeToSingleFile">True = one multi-page PDF; false = one PDF per layout.</param>
        /// <returns>A result carrying the full paths of the produced PDF file(s).</returns>
        OperationResult<IReadOnlyList<string>> Publish(
            IReadOnlyList<string> layoutNames,
            string outputFolder,
            string outputFileName,
            string pageSetupName,
            int dpi,
            bool mergeToSingleFile);
    }
}
