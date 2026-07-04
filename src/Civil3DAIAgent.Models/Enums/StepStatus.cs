namespace Civil3DAIAgent.Models.Enums
{
    /// <summary>
    /// Lifecycle state of a single workflow step. Drives UI colouring and run-summary reporting.
    /// </summary>
    public enum StepStatus
    {
        /// <summary>The step has not started yet.</summary>
        Pending = 0,

        /// <summary>The step is currently executing.</summary>
        Running = 1,

        /// <summary>The step finished successfully.</summary>
        Succeeded = 2,

        /// <summary>The step finished but raised non-fatal warnings (partial success).</summary>
        CompletedWithWarnings = 3,

        /// <summary>The step failed. Depending on configuration the workflow may continue or abort.</summary>
        Failed = 4,

        /// <summary>The step was skipped (e.g. optional input missing, or disabled in settings).</summary>
        Skipped = 5,

        /// <summary>The step was cancelled by the user before completing.</summary>
        Cancelled = 6
    }
}
