namespace Civil3DAIAgent.Models.Workflow
{
    /// <summary>
    /// The user-supplied inputs for a single automation run, captured by the UI and handed to the
    /// Application layer's workflow engine. This is a plain data object with no behaviour so it can
    /// cross layer boundaries freely.
    /// </summary>
    public sealed class WorkflowRequest
    {
        /// <summary>Full path to the source DWG containing the road polyline and contours. Required.</summary>
        public string InputDwgPath { get; set; } = "";

        /// <summary>Full path to the Excel points file. Optional (may be empty).</summary>
        public string InputExcelPath { get; set; } = "";

        /// <summary>Folder where all outputs (DWG, PDF, sheets, logs) are written. Required.</summary>
        public string OutputFolder { get; set; } = "";

        /// <summary>
        /// Optional override for the extraction length in metres. When &lt;= 0 the value from
        /// <c>appsettings.json</c> (<c>Extraction.SegmentLengthMeters</c>) is used instead.
        /// </summary>
        public double SegmentLengthMetersOverride { get; set; } = 0.0;

        /// <summary>
        /// Optional handle of the road polyline chosen interactively in the UI. When empty the engine
        /// auto-detects the longest polyline on a configured road layer.
        /// </summary>
        public string SelectedPolylineHandle { get; set; } = "";
    }
}
