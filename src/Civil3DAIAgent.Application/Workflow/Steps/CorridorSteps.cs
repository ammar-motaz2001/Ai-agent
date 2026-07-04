using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow.Steps
{
    /// <summary>Step 11 — obtain the corridor assembly (typical cross-section).</summary>
    public sealed class CreateAssemblyStep : WorkflowStepBase
    {
        private readonly IAssemblyService _assembly;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateAssemblyStep(IAssemblyService assembly, ICivilDocProvider docs) { _assembly = assembly; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateAssembly;
        /// <inheritdoc />
        public override string DisplayName => "Create Assembly";
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _assembly.GetOrCreateAssembly(_docs.ActiveDocument, context.Settings.Assembly);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.AssemblyHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 12 — build the corridor from alignment, design profile, and assembly.</summary>
    public sealed class CreateCorridorStep : WorkflowStepBase
    {
        private readonly ICorridorService _corridor;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateCorridorStep(ICorridorService corridor, ICivilDocProvider docs) { _corridor = corridor; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateCorridor;
        /// <inheritdoc />
        public override string DisplayName => "Create Corridor";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) =>
            context.Contains(WorkflowContextKeys.AlignmentHandle) &&
            context.Contains(WorkflowContextKeys.DesignProfileHandle) &&
            context.Contains(WorkflowContextKeys.AssemblyHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _corridor.CreateCorridor(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.AlignmentHandle),
                context.Get<string>(WorkflowContextKeys.DesignProfileHandle),
                context.Get<string>(WorkflowContextKeys.AssemblyHandle),
                context.Settings.Corridor);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.CorridorHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 13 — extract the corridor Top surface.</summary>
    public sealed class CreateTopSurfaceStep : WorkflowStepBase
    {
        private readonly ICorridorService _corridor;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateTopSurfaceStep(ICorridorService corridor, ICivilDocProvider docs) { _corridor = corridor; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateTopSurface;
        /// <inheritdoc />
        public override string DisplayName => "Create Top Surface";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.CorridorHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _corridor.CreateTopSurface(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.CorridorHandle),
                context.Settings.Surface.TopSurfaceName);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.TopSurfaceHandle, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }

    /// <summary>Step 14 — extract the corridor Datum surface.</summary>
    public sealed class CreateDatumSurfaceStep : WorkflowStepBase
    {
        private readonly ICorridorService _corridor;
        private readonly ICivilDocProvider _docs;
        /// <summary>Creates the step.</summary>
        public CreateDatumSurfaceStep(ICorridorService corridor, ICivilDocProvider docs) { _corridor = corridor; _docs = docs; }
        /// <inheritdoc />
        public override WorkflowStepType StepType => WorkflowStepType.CreateDatumSurface;
        /// <inheritdoc />
        public override string DisplayName => "Create Datum Surface";
        /// <inheritdoc />
        public override bool CanExecute(IWorkflowContext context) => context.Contains(WorkflowContextKeys.CorridorHandle);
        /// <inheritdoc />
        public override OperationResult Execute(IWorkflowContext context)
        {
            var res = _corridor.CreateDatumSurface(
                _docs.ActiveDocument,
                context.Get<string>(WorkflowContextKeys.CorridorHandle),
                context.Settings.Surface.DatumSurfaceName);
            if (res.Failed) return res;
            context.Set(WorkflowContextKeys.DatumSurfaceHandle, res.Value);
            context.Set(WorkflowContextKeys.DatumSurfaceName, res.Value);
            return OperationResult.Ok(res.Message, res.Warnings);
        }
    }
}
