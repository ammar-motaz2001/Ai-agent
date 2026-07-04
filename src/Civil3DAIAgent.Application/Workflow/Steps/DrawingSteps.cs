using System;
using Autodesk.AutoCAD.DatabaseServices;
using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Geometry;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow.Steps
{
    /// <summary>Step 1 — open the source DWG as a side database (kept alive for the run).</summary>
    public sealed class OpenSourceDrawingStep : WorkflowStepBase
    {
        private readonly IDrawingService _drawing;
        /// <summary>Creates the step.</summary>
        public OpenSourceDrawingStep(IDrawingService drawing) { _drawing = drawing; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.OpenSourceDrawing;
        /// <inheritdoc />
        public override string DisplayName => "Open Source Drawing";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _drawing.OpenSourceDatabase(context.Request.InputDwgPath);
            if (res.Failed) return res;

            context.RegisterForDisposal(res.Value);                 // dispose the side DB at run end
            context.Set(WorkflowContextKeys.SourceDatabase, res.Value);
            context.Set(WorkflowContextKeys.SourceDrawingPath, context.Request.InputDwgPath);
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 2 — select (or auto-detect) the road centreline polyline.</summary>
    public sealed class SelectRoadPolylineStep : WorkflowStepBase
    {
        private readonly IDrawingService _drawing;
        /// <summary>Creates the step.</summary>
        public SelectRoadPolylineStep(IDrawingService drawing) { _drawing = drawing; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.SelectRoadPolyline;
        /// <inheritdoc />
        public override string DisplayName => "Select Road Polyline";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SourceDatabase);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var db = context.Get<Database>(WorkflowContextKeys.SourceDatabase);
            var res = _drawing.SelectRoadPolyline(db, context.Request.SelectedPolylineHandle,
                context.Settings.Extraction.RoadPolylineLayers);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.SourcePolylineHandle, res.Value);
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 3 — extract the first N km of the road polyline.</summary>
    public sealed class ExtractFirstSegmentStep : WorkflowStepBase
    {
        private readonly IDrawingService _drawing;
        /// <summary>Creates the step.</summary>
        public ExtractFirstSegmentStep(IDrawingService drawing) { _drawing = drawing; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.ExtractFirstSegment;
        /// <inheritdoc />
        public override string DisplayName => "Extract First Segment";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SourcePolylineHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var db = context.Get<Database>(WorkflowContextKeys.SourceDatabase);
            var handle = context.Get<string>(WorkflowContextKeys.SourcePolylineHandle);
            var res = _drawing.ExtractFirstSegment(db, handle, ResolveSegmentLength(context));
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.ExtractedPolyline, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 4 — create the new drawing from the template and make it active.</summary>
    public sealed class CreateNewDrawingStep : WorkflowStepBase
    {
        private readonly IDrawingService _drawing;
        /// <summary>Creates the step.</summary>
        public CreateNewDrawingStep(IDrawingService drawing) { _drawing = drawing; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateNewDrawing;
        /// <inheritdoc />
        public override string DisplayName => "Create New Drawing";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _drawing.CreateNewDrawing(context.Settings.Paths.DrawingTemplate);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.NewDrawingPath, res.Value?.Name ?? "");
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 5 — paste the extracted polyline into the new drawing at original coordinates.</summary>
    public sealed class PastePolylineStep : WorkflowStepBase
    {
        /// <summary>Layer created in the new drawing to host the pasted road centreline.</summary>
        public const string RoadLayer = "AI-ROAD-CL";

        private readonly IDrawingService _drawing;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public PastePolylineStep(IDrawingService drawing, ICivilDocProvider docs) { _drawing = drawing; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.PastePolyline;
        /// <inheritdoc />
        public override string DisplayName => "Paste Polyline";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.ExtractedPolyline);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var data = context.Get<PolylineData>(WorkflowContextKeys.ExtractedPolyline);
            var res = _drawing.PastePolyline(_docs.ActiveDocument, data, RoadLayer);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.PastedPolylineHandle, res.Value);
            return OperationResult.Ok(res.Message);
        }
    }

    /// <summary>Step 6 — copy contour entities from the source into the new drawing.</summary>
    public sealed class CopyContoursStep : WorkflowStepBase
    {
        private readonly IDrawingService _drawing;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CopyContoursStep(IDrawingService drawing, ICivilDocProvider docs) { _drawing = drawing; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CopyContours;
        /// <inheritdoc />
        public override string DisplayName => "Copy Contours";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SourceDatabase);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var db = context.Get<Database>(WorkflowContextKeys.SourceDatabase);
            var res = _drawing.CopyContours(db, _docs.ActiveDocument, context.Settings.Extraction.ContourLayers);
            return res.Succeeded ? OperationResult.Ok(res.Message) : (OperationResult)res;
        }
    }
}
