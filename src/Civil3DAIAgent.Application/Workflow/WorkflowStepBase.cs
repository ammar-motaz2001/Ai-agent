using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Application.Workflow
{
    /// <summary>
    /// Base class for the 23 workflow steps. Provides the common shape (step type, display name,
    /// default "always executes") and small helpers, so each concrete step only implements its
    /// <see cref="Execute"/> body. Cross-cutting concerns (timing, try/catch, logging, progress) live
    /// in the engine, not here, keeping steps focused on a single service call.
    /// </summary>
    public abstract class WorkflowStepBase : IWorkflowStep
    {
        /// <inheritdoc />
        public abstract WorkflowStepType StepType { get; }

        /// <inheritdoc />
        public abstract string DisplayName { get; }

        /// <inheritdoc />
        public virtual bool CanExecute(IWorkflowContext context) => true;

        /// <inheritdoc />
        public abstract OperationResult Execute(IWorkflowContext context);

        /// <summary>Throws if the run has been cancelled. Call at the start of long steps.</summary>
        protected static void ThrowIfCancelled(IWorkflowContext context)
            => context.CancellationToken.ThrowIfCancellationRequested();

        /// <summary>Resolves the effective extraction length (request override, else settings).</summary>
        protected static double ResolveSegmentLength(IWorkflowContext context)
        {
            var overrideValue = context.Request.SegmentLengthMetersOverride;
            return overrideValue > 0 ? overrideValue : context.Settings.Extraction.SegmentLengthMeters;
        }
    }
}
