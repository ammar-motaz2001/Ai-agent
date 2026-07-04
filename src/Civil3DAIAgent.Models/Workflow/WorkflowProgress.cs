using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Models.Workflow
{
    /// <summary>
    /// A single progress notification pushed from the workflow engine to the UI via
    /// <see cref="System.IProgress{T}"/>. Immutable snapshot so it can be marshalled safely across
    /// the background/UI thread boundary.
    /// </summary>
    public sealed class WorkflowProgress
    {
        /// <summary>Creates a progress snapshot.</summary>
        public WorkflowProgress(int completedSteps, int totalSteps, WorkflowStepType currentStep,
            string currentStepName, StepStatus currentStatus)
        {
            CompletedSteps = completedSteps;
            TotalSteps = totalSteps;
            CurrentStep = currentStep;
            CurrentStepName = currentStepName ?? string.Empty;
            CurrentStatus = currentStatus;
        }

        /// <summary>Number of steps finished so far.</summary>
        public int CompletedSteps { get; }

        /// <summary>Total number of steps in the run.</summary>
        public int TotalSteps { get; }

        /// <summary>The step currently being reported on.</summary>
        public WorkflowStepType CurrentStep { get; }

        /// <summary>Friendly name of the current step.</summary>
        public string CurrentStepName { get; }

        /// <summary>Status of the current step at the moment this snapshot was created.</summary>
        public StepStatus CurrentStatus { get; }

        /// <summary>Percent complete in the range 0-100.</summary>
        public double PercentComplete =>
            TotalSteps <= 0 ? 0.0 : (CompletedSteps * 100.0) / TotalSteps;
    }
}
