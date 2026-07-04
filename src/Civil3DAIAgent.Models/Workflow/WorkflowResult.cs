using System;
using System.Collections.Generic;
using System.Linq;

namespace Civil3DAIAgent.Models.Workflow
{
    /// <summary>
    /// Aggregate outcome of a full automation run: the ordered list of per-step results plus
    /// convenience roll-ups used by the UI summary panel and the exported run report.
    /// </summary>
    public sealed class WorkflowResult
    {
        /// <summary>Per-step results, in execution order.</summary>
        public List<StepResult> Steps { get; } = new List<StepResult>();

        /// <summary>UTC time the run started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC time the run finished.</summary>
        public DateTime FinishedUtc { get; set; }

        /// <summary>Total wall-clock time of the run.</summary>
        public TimeSpan TotalDuration => FinishedUtc > StartedUtc ? FinishedUtc - StartedUtc : TimeSpan.Zero;

        /// <summary>True when the user cancelled the run before it completed.</summary>
        public bool WasCancelled { get; set; }

        /// <summary>Full path to the saved output DWG (empty if the save step did not run).</summary>
        public string OutputDwgPath { get; set; } = "";

        /// <summary>Full path(s) to generated PDF file(s).</summary>
        public List<string> OutputPdfPaths { get; } = new List<string>();

        /// <summary>Count of steps that failed.</summary>
        public int FailureCount => Steps.Count(s => !s.IsSuccessful);

        /// <summary>Count of steps that raised at least one warning.</summary>
        public int WarningCount => Steps.Count(s => s.Warnings.Count > 0);

        /// <summary>
        /// True only when every executed step ended successfully (or was intentionally skipped) and
        /// the run was not cancelled.
        /// </summary>
        public bool OverallSuccess => !WasCancelled && Steps.All(s => s.IsSuccessful);
    }
}
