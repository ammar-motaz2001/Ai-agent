using System.Collections.Generic;
using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow.Steps
{
    /// <summary>Step 15 — create the sample-line group and sample lines.</summary>
    public sealed class CreateSampleLinesStep : WorkflowStepBase
    {
        private readonly ISampleLineService _sampleLines;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateSampleLinesStep(ISampleLineService sampleLines, ICivilDocProvider docs) { _sampleLines = sampleLines; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateSampleLines;
        /// <inheritdoc />
        public override string DisplayName => "Create Sample Lines";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.AlignmentHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var surfaces = new List<string>();
            if (context.TryGet<string>(WorkflowContextKeys.ExistingGroundSurfaceHandle, out var eg)) surfaces.Add(eg);

            context.TryGet<string>(WorkflowContextKeys.CorridorHandle, out var corridor);

            var res = _sampleLines.CreateSampleLines(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.AlignmentHandle),
                context.Settings.SampleLines,
                surfaces,
                corridor);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.SampleLineGroupHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 16 — compute the material list (quantity takeoff).</summary>
    public sealed class ComputeMaterialsStep : WorkflowStepBase
    {
        private readonly IMaterialService _materials;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public ComputeMaterialsStep(IMaterialService materials, ICivilDocProvider docs) { _materials = materials; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.ComputeMaterials;
        /// <inheritdoc />
        public override string DisplayName => "Compute Materials";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SampleLineGroupHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            context.TryGet<string>(WorkflowContextKeys.ExistingGroundSurfaceHandle, out var eg);
            context.TryGet<string>(WorkflowContextKeys.DatumSurfaceName, out var datum);

            var res = _materials.ComputeMaterials(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.SampleLineGroupHandle),
                eg, datum, context.Settings.Materials);
            if (res.Failed) return res;

            context.Set(WorkflowContextKeys.MaterialListName, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 17 — compute cut &amp; fill volumes from the material list.</summary>
    public sealed class ComputeCutFillStep : WorkflowStepBase
    {
        private readonly IMaterialService _materials;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public ComputeCutFillStep(IMaterialService materials, ICivilDocProvider docs) { _materials = materials; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.ComputeCutFill;
        /// <inheritdoc />
        public override string DisplayName => "Compute Cut & Fill";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SampleLineGroupHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            context.TryGet<string>(WorkflowContextKeys.MaterialListName, out var listName);
            var res = _materials.ComputeCutFill(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.SampleLineGroupHandle),
                listName, context.Settings.Materials);
            if (res.Failed) return res;

            // The service already logged the volumes; surface them in the step message too.
            var summary = res.Value != null ? res.Value.ToDisplayString() : "no volumes";
            return OperationResult.Ok(summary, res.Warnings);
        }
    }

    /// <summary>Step 18 — create section views for each sample line.</summary>
    public sealed class CreateSectionViewsStep : WorkflowStepBase
    {
        private readonly ISectionViewService _sections;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateSectionViewsStep(ISectionViewService sections, ICivilDocProvider docs) { _sections = sections; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateSectionViews;
        /// <inheritdoc />
        public override string DisplayName => "Create Section Views";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.SampleLineGroupHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _sections.CreateSectionViews(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.SampleLineGroupHandle),
                context.Settings.Sheets);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.SectionViewHandles, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 19 — create profile view(s) for the alignment.</summary>
    public sealed class CreateProfileViewsStep : WorkflowStepBase
    {
        private readonly IProfileViewService _profileViews;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateProfileViewsStep(IProfileViewService profileViews, ICivilDocProvider docs) { _profileViews = profileViews; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateProfileViews;
        /// <inheritdoc />
        public override string DisplayName => "Create Profile Views";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.AlignmentHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _profileViews.CreateProfileViews(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.AlignmentHandle),
                context.Settings.Sheets);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.ProfileViewHandles, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }
}
