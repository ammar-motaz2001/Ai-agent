using System;
using System.Collections.Generic;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Models.Workflow
{
    /// <summary>
    /// The outcome of executing a single <see cref="WorkflowStepType"/>. Captures status, timing,
    /// messages and any warnings so the UI and the final run report can present a complete picture.
    /// </summary>
    public sealed class StepResult
    {
        /// <summary>Which step this result describes.</summary>
        public WorkflowStepType Step { get; set; }

        /// <summary>Friendly display name of the step (e.g. "Create Alignment").</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Final status of the step.</summary>
        public StepStatus Status { get; set; } = StepStatus.Pending;

        /// <summary>Primary message: a success note or a plain-language failure explanation.</summary>
        public string Message { get; set; } = "";

        /// <summary>Non-fatal warnings raised during the step.</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>UTC time the step started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC time the step finished.</summary>
        public DateTime FinishedUtc { get; set; }

        /// <summary>Wall-clock execution time.</summary>
        public TimeSpan Duration => FinishedUtc > StartedUtc ? FinishedUtc - StartedUtc : TimeSpan.Zero;

        /// <summary>The originating exception when the step failed because of one; otherwise null.</summary>
        public Exception Exception { get; set; }

        /// <summary>True when the step ended in a state considered "not a failure".</summary>
        public bool IsSuccessful =>
            Status == StepStatus.Succeeded ||
            Status == StepStatus.CompletedWithWarnings ||
            Status == StepStatus.Skipped;
    }
}
