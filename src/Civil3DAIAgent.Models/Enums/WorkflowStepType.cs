namespace Civil3DAIAgent.Models.Enums
{
    /// <summary>
    /// Enumerates every discrete operation the automation engine can perform, in the
    /// canonical order of a full road-design run. The numeric values are stable and are
    /// used for ordering, progress calculation, and persistence of run history.
    /// </summary>
    /// <remarks>
    /// Each value maps 1:1 to a step implementation in the Application layer
    /// (see <c>Civil3DAIAgent.Application.Workflow.Steps</c>). Adding a new step is done by
    /// appending a value here and registering the matching step class — no other layer
    /// needs to change.
    /// </remarks>
    public enum WorkflowStepType
    {
        /// <summary>Open the source DWG that contains the road polyline and contours.</summary>
        OpenSourceDrawing = 1,

        /// <summary>Select (or auto-detect) the polyline that represents the road centreline.</summary>
        SelectRoadPolyline = 2,

        /// <summary>Trim / extract the first N kilometres (default 3 km) of the road polyline.</summary>
        ExtractFirstSegment = 3,

        /// <summary>Create a brand-new drawing from the configured template.</summary>
        CreateNewDrawing = 4,

        /// <summary>Copy the extracted polyline into the new drawing preserving world coordinates.</summary>
        PastePolyline = 5,

        /// <summary>Copy contour objects (existing-ground contours) into the new drawing.</summary>
        CopyContours = 6,

        /// <summary>Create a Civil 3D Alignment from the pasted polyline.</summary>
        CreateAlignment = 7,

        /// <summary>Create the Existing Ground (EG) TIN surface from contours and/or survey points.</summary>
        CreateExistingGroundSurface = 8,

        /// <summary>Sample the EG surface along the alignment to create the existing-ground profile.</summary>
        CreateExistingGroundProfile = 9,

        /// <summary>Create the proposed / design profile by layout (PVIs and vertical curves).</summary>
        CreateDesignProfile = 10,

        /// <summary>Create the corridor assembly (lanes, shoulders, daylight subassemblies).</summary>
        CreateAssembly = 11,

        /// <summary>Build the corridor from the alignment, design profile, and assembly.</summary>
        CreateCorridor = 12,

        /// <summary>Extract the corridor Top surface.</summary>
        CreateTopSurface = 13,

        /// <summary>Extract the corridor Datum surface.</summary>
        CreateDatumSurface = 14,

        /// <summary>Create sample lines along the alignment for sectioning.</summary>
        CreateSampleLines = 15,

        /// <summary>Compute material (quantity takeoff) volumes between surfaces.</summary>
        ComputeMaterials = 16,

        /// <summary>Compute cut &amp; fill volumes between EG and datum surfaces.</summary>
        ComputeCutFill = 17,

        /// <summary>Create section views for each sample line station.</summary>
        CreateSectionViews = 18,

        /// <summary>Create the profile view(s) for the alignment.</summary>
        CreateProfileViews = 19,

        /// <summary>Generate paper-space layout sheets (plan, profile, sections).</summary>
        GenerateLayoutSheets = 20,

        /// <summary>Create a sheet set (.dst) referencing the generated layouts.</summary>
        CreateSheetSet = 21,

        /// <summary>Publish / export the sheets to PDF.</summary>
        GeneratePdfs = 22,

        /// <summary>Save the final DWG to the output folder.</summary>
        SaveDrawing = 23
    }
}
