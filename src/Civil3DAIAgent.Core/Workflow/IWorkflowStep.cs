using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Core.Workflow
{
    /// <summary>
    /// Contract for a single unit of work in the automation pipeline (open drawing, create alignment,
    /// build corridor, ...). Each of the 23 operations is one implementation. The engine discovers
    /// steps via DI, orders them by <see cref="StepType"/>, and runs each inside its own try/catch and
    /// timing scope, so individual steps never need to handle their own timing or top-level crashes.
    /// </summary>
    public interface IWorkflowStep
    {
        /// <summary>Which workflow operation this step implements (also defines execution order).</summary>
        WorkflowStepType StepType { get; }

        /// <summary>Friendly name shown in the UI and logs (e.g. "Create Alignment").</summary>
        string DisplayName { get; }

        /// <summary>
        /// Returns false when the step should be skipped for this run (e.g. optional Excel input not
        /// provided, or a prerequisite artefact missing). The engine records a
        /// <see cref="StepStatus.Skipped"/> instead of executing.
        /// </summary>
        bool CanExecute(IWorkflowContext context);

        /// <summary>
        /// Performs the operation. Implementations should return a failed <see cref="OperationResult"/>
        /// for handled errors and may throw for unexpected ones; either way the engine records the
        /// outcome, logs an explanation, and (per configuration) continues or aborts. Long operations
        /// must poll <see cref="IWorkflowContext.CancellationToken"/>.
        /// </summary>
        OperationResult Execute(IWorkflowContext context);
    }
}
