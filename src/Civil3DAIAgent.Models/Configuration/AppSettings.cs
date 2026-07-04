using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Models.Configuration
{
    /// <summary>
    /// Strongly-typed representation of <c>appsettings.json</c>. This is the single source of truth
    /// for every tunable parameter in the automation. It is loaded once at start-up by the
    /// Infrastructure layer's configuration service and injected wherever settings are needed.
    /// Every property has a safe default so a missing/partial JSON file never breaks the run.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>Default file-system locations used to pre-populate the UI.</summary>
        public PathSettings Paths { get; set; } = new PathSettings();

        /// <summary>Excel parsing options (sheet, header row, column mapping).</summary>
        public ExcelSettings Excel { get; set; } = new ExcelSettings();

        /// <summary>Polyline extraction options (segment length, layers).</summary>
        public ExtractionSettings Extraction { get; set; } = new ExtractionSettings();

        /// <summary>Alignment creation options.</summary>
        public AlignmentSettings Alignment { get; set; } = new AlignmentSettings();

        /// <summary>Existing-ground surface options.</summary>
        public SurfaceSettings Surface { get; set; } = new SurfaceSettings();

        /// <summary>Profile creation options.</summary>
        public ProfileSettings Profile { get; set; } = new ProfileSettings();

        /// <summary>Assembly (typical cross-section) options.</summary>
        public AssemblySettings Assembly { get; set; } = new AssemblySettings();

        /// <summary>Corridor options.</summary>
        public CorridorSettings Corridor { get; set; } = new CorridorSettings();

        /// <summary>Sample-line options.</summary>
        public SampleLineSettings SampleLines { get; set; } = new SampleLineSettings();

        /// <summary>Material / cut-fill computation options.</summary>
        public MaterialSettings Materials { get; set; } = new MaterialSettings();

        /// <summary>Sheet / layout generation options.</summary>
        public SheetSettings Sheets { get; set; } = new SheetSettings();

        /// <summary>PDF publishing options.</summary>
        public PdfSettings Pdf { get; set; } = new PdfSettings();

        /// <summary>Logging options.</summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>When true, a failed non-critical step does not abort the whole run.</summary>
        public bool ContinueOnStepFailure { get; set; } = true;
    }

    /// <summary>Default file-system locations.</summary>
    public sealed class PathSettings
    {
        /// <summary>Default source DWG shown in the UI file picker.</summary>
        public string DefaultInputDwg { get; set; } = "";

        /// <summary>Default Excel points file (optional).</summary>
        public string DefaultInputExcel { get; set; } = "";

        /// <summary>Default output folder for generated DWG/PDF/sheets.</summary>
        public string DefaultOutputFolder { get; set; } = "";

        /// <summary>Path to the DWT template used when creating the new drawing.</summary>
        public string DrawingTemplate { get; set; } = "";
    }

    /// <summary>Excel parsing configuration.</summary>
    public sealed class ExcelSettings
    {
        /// <summary>Worksheet name to read; empty means "first sheet".</summary>
        public string SheetName { get; set; } = "";

        /// <summary>1-based row index that contains column headers (0 = no header row).</summary>
        public int HeaderRow { get; set; } = 1;

        /// <summary>Header text (or column letter) that holds the point number.</summary>
        public string PointNumberColumn { get; set; } = "POINT";

        /// <summary>Header text (or column letter) that holds the Easting (X).</summary>
        public string EastingColumn { get; set; } = "EASTING";

        /// <summary>Header text (or column letter) that holds the Northing (Y).</summary>
        public string NorthingColumn { get; set; } = "NORTHING";

        /// <summary>Header text (or column letter) that holds the Elevation (Z).</summary>
        public string ElevationColumn { get; set; } = "ELEVATION";

        /// <summary>Header text (or column letter) that holds the description (empty = none).</summary>
        public string DescriptionColumn { get; set; } = "";
    }

    /// <summary>Polyline extraction configuration.</summary>
    public sealed class ExtractionSettings
    {
        /// <summary>Length of road to extract from the start of the selected polyline, in metres.</summary>
        public double SegmentLengthMeters { get; set; } = 3000.0;

        /// <summary>Layer name(s) that identify the road polyline for auto-detection (comma-separated).</summary>
        public string RoadPolylineLayers { get; set; } = "ROAD,CENTERLINE,CL";

        /// <summary>Layer name(s) that identify contour objects to copy (comma-separated).</summary>
        public string ContourLayers { get; set; } = "CONTOUR,CONTOURS,EG-CONTOUR";
    }

    /// <summary>Alignment configuration.</summary>
    public sealed class AlignmentSettings
    {
        /// <summary>Base name for the created alignment.</summary>
        public string Name { get; set; } = "AI-Alignment-01";

        /// <summary>Alignment style name (must exist in the template).</summary>
        public string StyleName { get; set; } = "Proposed";

        /// <summary>Alignment label-set style name (must exist in the template).</summary>
        public string LabelSetName { get; set; } = "All Labels";
    }

    /// <summary>Existing-ground surface configuration.</summary>
    public sealed class SurfaceSettings
    {
        /// <summary>Name of the existing-ground surface.</summary>
        public string ExistingGroundName { get; set; } = "EG-Surface";

        /// <summary>Surface style name (must exist in the template).</summary>
        public string StyleName { get; set; } = "Contours 1m and 5m (Background)";

        /// <summary>When true, add survey points from the Excel file to the surface.</summary>
        public bool BuildFromExcelPoints { get; set; } = true;

        /// <summary>When true, add copied contours to the surface.</summary>
        public bool BuildFromContours { get; set; } = true;

        /// <summary>Name for the extracted corridor top surface.</summary>
        public string TopSurfaceName { get; set; } = "Corridor-Top";

        /// <summary>Name for the extracted corridor datum surface.</summary>
        public string DatumSurfaceName { get; set; } = "Corridor-Datum";
    }

    /// <summary>Profile configuration.</summary>
    public sealed class ProfileSettings
    {
        /// <summary>Name of the existing-ground (sampled) profile.</summary>
        public string ExistingGroundName { get; set; } = "EG-Profile";

        /// <summary>Name of the design profile.</summary>
        public string DesignName { get; set; } = "Design-Profile";

        /// <summary>EG profile style name.</summary>
        public string ExistingGroundStyle { get; set; } = "Existing Ground Profile";

        /// <summary>Design profile style name.</summary>
        public string DesignStyle { get; set; } = "Design Profile";

        /// <summary>
        /// When true the design profile is generated automatically as a smoothed offset of the EG
        /// profile (a sensible default when no engineer-supplied grades exist). When false the design
        /// profile is created empty for later manual editing.
        /// </summary>
        public bool AutoGenerateFromExistingGround { get; set; } = true;

        /// <summary>Vertical offset (metres) applied to EG when auto-generating the design profile.</summary>
        public double AutoGenerateVerticalOffset { get; set; } = 0.0;

        /// <summary>Default parabolic vertical-curve length (metres) used at auto-generated PVIs.</summary>
        public double DefaultCurveLength { get; set; } = 100.0;

        /// <summary>Station interval (metres) at which PVIs are sampled when auto-generating.</summary>
        public double PviSampleInterval { get; set; } = 100.0;
    }

    /// <summary>Assembly configuration (a simple crowned two-lane road with daylight by default).</summary>
    public sealed class AssemblySettings
    {
        /// <summary>Assembly name.</summary>
        public string Name { get; set; } = "AI-Assembly-01";

        /// <summary>Lane width per side, metres.</summary>
        public double LaneWidth { get; set; } = 3.65;

        /// <summary>Number of lanes per side.</summary>
        public int LanesPerSide { get; set; } = 1;

        /// <summary>Lane cross-slope, percent (negative = falls away from crown).</summary>
        public double LaneSlopePercent { get; set; } = -2.0;

        /// <summary>Shoulder width per side, metres.</summary>
        public double ShoulderWidth { get; set; } = 1.5;

        /// <summary>Shoulder cross-slope, percent.</summary>
        public double ShoulderSlopePercent { get; set; } = -4.0;

        /// <summary>Cut daylight slope (rise:run as run per 1 rise). E.g. 2 = 2:1.</summary>
        public double CutSlope { get; set; } = 2.0;

        /// <summary>Fill daylight slope (run per 1 rise).</summary>
        public double FillSlope { get; set; } = 3.0;

        /// <summary>Depth of pavement/subgrade structure below finished grade, metres.</summary>
        public double SubgradeDepth { get; set; } = 0.5;
    }

    /// <summary>Corridor configuration.</summary>
    public sealed class CorridorSettings
    {
        /// <summary>Corridor name.</summary>
        public string Name { get; set; } = "AI-Corridor-01";

        /// <summary>Assembly application frequency along tangents, metres.</summary>
        public double FrequencyTangent { get; set; } = 20.0;

        /// <summary>Assembly application frequency along curves, metres.</summary>
        public double FrequencyCurve { get; set; } = 10.0;
    }

    /// <summary>Sample-line configuration.</summary>
    public sealed class SampleLineSettings
    {
        /// <summary>Sample-line group name.</summary>
        public string GroupName { get; set; } = "AI-SL-Group";

        /// <summary>Station interval between sample lines, metres.</summary>
        public double Interval { get; set; } = 25.0;

        /// <summary>Swath width to the left of the alignment, metres.</summary>
        public double SwathWidthLeft { get; set; } = 20.0;

        /// <summary>Swath width to the right of the alignment, metres.</summary>
        public double SwathWidthRight { get; set; } = 20.0;
    }

    /// <summary>Material / cut-fill configuration.</summary>
    public sealed class MaterialSettings
    {
        /// <summary>Quantity-takeoff criteria name (must exist in the template).</summary>
        public string QuantityTakeoffCriteria { get; set; } = "Cut and Fill";

        /// <summary>Cut factor (expansion) applied to cut volumes.</summary>
        public double CutFactor { get; set; } = 1.0;

        /// <summary>Fill factor (compaction) applied to fill volumes.</summary>
        public double FillFactor { get; set; } = 1.0;
    }

    /// <summary>Sheet / layout configuration.</summary>
    public sealed class SheetSettings
    {
        /// <summary>Sheet-generation template DWT that contains the required layout/view-frame styles.</summary>
        public string SheetTemplate { get; set; } = "";

        /// <summary>Named page-setup (in the template) used for layouts and plotting.</summary>
        public string PageSetupName { get; set; } = "PDF-A1";

        /// <summary>Plan-view horizontal scale (1:Scale).</summary>
        public double PlanScale { get; set; } = 1000.0;

        /// <summary>Profile-view horizontal scale (1:Scale).</summary>
        public double ProfileScale { get; set; } = 1000.0;

        /// <summary>Section-view scale (1:Scale).</summary>
        public double SectionScale { get; set; } = 100.0;

        /// <summary>Number of section views arranged per sheet.</summary>
        public int SectionsPerSheet { get; set; } = 6;
    }

    /// <summary>PDF publishing configuration.</summary>
    public sealed class PdfSettings
    {
        /// <summary>File-name pattern for the combined PDF. Supports {drawing} and {date} tokens.</summary>
        public string OutputFileName { get; set; } = "{drawing}_sheets.pdf";

        /// <summary>Plot resolution in DPI.</summary>
        public int Dpi { get; set; } = 400;

        /// <summary>When true, merge all sheets into a single multi-page PDF; otherwise one PDF per sheet.</summary>
        public bool MergeToSingleFile { get; set; } = true;
    }

    /// <summary>Logging configuration.</summary>
    public sealed class LoggingSettings
    {
        /// <summary>Minimum level that will be written. Anything below is discarded.</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        /// <summary>Folder where log files are written. Empty means &lt;output&gt;\logs.</summary>
        public string LogFolder { get; set; } = "";

        /// <summary>When true, also write to a per-run log file (in addition to the UI window).</summary>
        public bool WriteToFile { get; set; } = true;

        /// <summary>Number of days to keep old log files before they are purged.</summary>
        public int RetainDays { get; set; } = 30;
    }
}
