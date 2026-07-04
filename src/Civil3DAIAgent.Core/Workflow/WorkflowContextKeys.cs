namespace Civil3DAIAgent.Core.Workflow
{
    /// <summary>
    /// Well-known keys for values passed between workflow steps via <see cref="IWorkflowContext"/>.
    /// Centralizing them here prevents typo-driven bugs and documents the data flow through the run.
    /// </summary>
    /// <remarks>
    /// References to Civil 3D objects are stored as AutoCAD <b>handle strings</b> (not live
    /// <c>ObjectId</c>s). Handles are persistent within a drawing and resolvable on demand, which lets
    /// the Application/orchestration layer remain completely free of Autodesk types — only the Civil3D
    /// layer converts handles to <c>ObjectId</c>s. The only exception is the live source
    /// <c>Database</c>, which is stored as an object and registered for disposal at run end.
    /// </remarks>
    public static class WorkflowContextKeys
    {
        /// <summary>Full path of the source DWG that was opened. (string)</summary>
        public const string SourceDrawingPath = "SourceDrawingPath";

        /// <summary>The live, read-only side <c>Database</c> of the source drawing. (Autodesk Database)</summary>
        public const string SourceDatabase = "SourceDatabase";

        /// <summary>Handle of the road polyline selected/detected in the source drawing. (string)</summary>
        public const string SourcePolylineHandle = "SourcePolylineHandle";

        /// <summary>Extracted road-centreline geometry for cross-drawing transfer. (PolylineData)</summary>
        public const string ExtractedPolyline = "ExtractedPolyline";

        /// <summary>Full path of the newly created output DWG. (string)</summary>
        public const string NewDrawingPath = "NewDrawingPath";

        /// <summary>Handle of the pasted road polyline in the new drawing. (string)</summary>
        public const string PastedPolylineHandle = "PastedPolylineHandle";

        /// <summary>Handle of the created alignment. (string)</summary>
        public const string AlignmentHandle = "AlignmentHandle";

        /// <summary>Handle of the existing-ground surface. (string)</summary>
        public const string ExistingGroundSurfaceHandle = "ExistingGroundSurfaceHandle";

        /// <summary>Handle of the existing-ground profile. (string)</summary>
        public const string ExistingGroundProfileHandle = "ExistingGroundProfileHandle";

        /// <summary>Handle of the design profile. (string)</summary>
        public const string DesignProfileHandle = "DesignProfileHandle";

        /// <summary>Handle of the assembly. (string)</summary>
        public const string AssemblyHandle = "AssemblyHandle";

        /// <summary>Handle of the corridor. (string)</summary>
        public const string CorridorHandle = "CorridorHandle";

        /// <summary>Handle of the corridor top surface. (string)</summary>
        public const string TopSurfaceHandle = "TopSurfaceHandle";

        /// <summary>Handle of the corridor datum surface. (string)</summary>
        public const string DatumSurfaceHandle = "DatumSurfaceHandle";

        /// <summary>Handle of the sample-line group. (string)</summary>
        public const string SampleLineGroupHandle = "SampleLineGroupHandle";

        /// <summary>Handles of created profile views. (List&lt;string&gt;)</summary>
        public const string ProfileViewHandles = "ProfileViewHandles";

        /// <summary>Handles of created section views. (List&lt;string&gt;)</summary>
        public const string SectionViewHandles = "SectionViewHandles";

        /// <summary>Names of paper-space layouts generated for plotting. (List&lt;string&gt;)</summary>
        public const string LayoutNames = "LayoutNames";

        /// <summary>Full path of the created sheet set (.dst). (string)</summary>
        public const string SheetSetPath = "SheetSetPath";

        /// <summary>Parsed survey points from the Excel input. (IReadOnlyList&lt;SurveyPoint&gt;)</summary>
        public const string SurveyPoints = "SurveyPoints";

        /// <summary>Name of the created material list. (string)</summary>
        public const string MaterialListName = "MaterialListName";

        /// <summary>Name of the corridor datum surface (referenced by name). (string)</summary>
        public const string DatumSurfaceName = "DatumSurfaceName";

        /// <summary>Full path of the final saved DWG. (string)</summary>
        public const string SavedDrawingPath = "SavedDrawingPath";

        /// <summary>Full paths of the generated PDF files. (IReadOnlyList&lt;string&gt;)</summary>
        public const string PdfOutputPaths = "PdfOutputPaths";
    }
}
