using System.Collections.Generic;
using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow.Steps
{
    /// <summary>Step 7 — create the alignment from the pasted polyline.</summary>
    public sealed class CreateAlignmentStep : WorkflowStepBase
    {
        private readonly IAlignmentService _alignment;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateAlignmentStep(IAlignmentService alignment, ICivilDocProvider docs) { _alignment = alignment; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateAlignment;
        /// <inheritdoc />
        public override string DisplayName => "Create Alignment";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.PastedPolylineHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var handle = context.Get<string>(WorkflowContextKeys.PastedPolylineHandle);
            var res = _alignment.CreateFromPolyline(_docs.ActiveDocument, handle, context.Settings.Alignment);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.AlignmentHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 8 — create the existing-ground surface from contours and (optional) Excel points.</summary>
    public sealed class CreateExistingGroundSurfaceStep : WorkflowStepBase
    {
        private readonly ISurfaceService _surface;
        private readonly IExcelPointReader _excel;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateExistingGroundSurfaceStep(ISurfaceService surface, IExcelPointReader excel, ICivilDocProvider docs)
        { _surface = surface; _excel = excel; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateExistingGroundSurface;
        /// <inheritdoc />
        public override string DisplayName => "Create Existing Ground Surface";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var warnings = new List<string>();

            // Read the optional Excel points (a failure here is non-fatal — the surface can use contours).
            IReadOnlyList<SurveyPoint> points = new List<SurveyPoint>();
            var read = _excel.ReadPoints(context.Request.InputExcelPath, context.Settings.Excel);
            if (read.Succeeded)
            {
                points = read.Value;
                context.Set(WorkflowContextKeys.SurveyPoints, points);
                foreach (var w in read.Warnings) warnings.Add(w);
            }
            else
            {
                warnings.Add("Excel points were not loaded: " + read.Message);
            }

            var res = _surface.CreateExistingGround(_docs.ActiveDocument, points,
                context.Settings.Surface, context.Settings.Extraction.ContourLayers);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.ExistingGroundSurfaceHandle, res.Value);
            warnings.AddRange(res.Warnings);
            return OperationResult.Ok(res.Message, warnings);
        }
    }

    /// <summary>Step 9 — create the existing-ground profile by sampling the surface along the alignment.</summary>
    public sealed class CreateExistingGroundProfileStep : WorkflowStepBase
    {
        private readonly IProfileService _profile;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateExistingGroundProfileStep(IProfileService profile, ICivilDocProvider docs) { _profile = profile; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateExistingGroundProfile;
        /// <inheritdoc />
        public override string DisplayName => "Create Existing Ground Profile";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) =>
            context.Contains(WorkflowContextKeys.AlignmentHandle) &&
            context.Contains(WorkflowContextKeys.ExistingGroundSurfaceHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _profile.CreateExistingGroundProfile(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.AlignmentHandle),
                context.Get<string>(WorkflowContextKeys.ExistingGroundSurfaceHandle),
                context.Settings.Profile);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.ExistingGroundProfileHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 10 — create the design profile by layout (auto-seeded from the surface).</summary>
    public sealed class CreateDesignProfileStep : WorkflowStepBase
    {
        private readonly IProfileService _profile;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateDesignProfileStep(IProfileService profile, ICivilDocProvider docs) { _profile = profile; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateDesignProfile;
        /// <inheritdoc />
        public override string DisplayName => "Create Design Profile";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.AlignmentHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            context.TryGet<string>(WorkflowContextKeys.ExistingGroundSurfaceHandle, out var egSurface);
            var res = _profile.CreateDesignProfile(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.AlignmentHandle),
                egSurface,
                context.Settings.Profile);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.DesignProfileHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }
}
