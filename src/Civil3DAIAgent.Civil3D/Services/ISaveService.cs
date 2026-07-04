using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Civil3D.Services
{
    /// <summary>Saves the finished drawing to disk (step 23).</summary>
    public interface ISaveService
    {
        /// <summary>
        /// Saves <paramref name="targetDocument"/> to <paramref name="outputFolder"/> using
        /// <paramref name="fileName"/> (a .dwg extension is ensured). Returns the saved full path.
        /// </summary>
        OperationResult<string> SaveDrawing(Document targetDocument, string outputFolder, string fileName);
    }
}
