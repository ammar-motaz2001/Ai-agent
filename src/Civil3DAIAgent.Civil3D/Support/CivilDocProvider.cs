using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DAIAgent.Civil3D.Support
{
    /// <summary>
    /// Central, testable-behind-an-interface access point to the currently active Civil 3D document
    /// and its related objects (Database, Editor, CivilDocument). Wrapping these statics in one place
    /// keeps the operation classes clean and makes the threading/document assumptions explicit.
    /// </summary>
    public interface ICivilDocProvider
    {
        /// <summary>The active AutoCAD document. Null when no document is open.</summary>
        Document ActiveDocument { get; }

        /// <summary>The database of the active document. Null when no document is open.</summary>
        Database ActiveDatabase { get; }

        /// <summary>The command-line editor of the active document. Null when no document is open.</summary>
        Editor ActiveEditor { get; }

        /// <summary>The Civil 3D document (styles, alignments, surfaces...) of the active drawing.</summary>
        CivilDocument ActiveCivilDocument { get; }

        /// <summary>
        /// Pins the document the workflow should operate on (set by the "create new drawing" step). Once
        /// set, <see cref="ActiveDocument"/> returns this document instead of relying on
        /// <c>MdiActiveDocument</c>, which can lag right after a new drawing is created.
        /// </summary>
        void SetActiveDocument(Document document);
    }

    /// <summary>
    /// Default <see cref="ICivilDocProvider"/> backed by the live AutoCAD/Civil 3D application. All
    /// members read the document manager on each call so the "active" document is always current.
    /// </summary>
    public sealed class CivilDocProvider : ICivilDocProvider
    {
        private Document _pinned;

        /// <inheritdoc />
        public Document ActiveDocument => _pinned ?? AcApp.DocumentManager?.MdiActiveDocument;

        /// <inheritdoc />
        public void SetActiveDocument(Document document) => _pinned = document;

        /// <inheritdoc />
        public Database ActiveDatabase => ActiveDocument?.Database;

        /// <inheritdoc />
        public Editor ActiveEditor => ActiveDocument?.Editor;

        /// <inheritdoc />
        public CivilDocument ActiveCivilDocument
        {
            get
            {
                var db = ActiveDatabase;
                return db == null ? null : CivilDocument.GetCivilDocument(db);
            }
        }
    }
}
