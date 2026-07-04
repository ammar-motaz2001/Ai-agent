using System;
using System.Threading;
using System.Threading.Tasks;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Core.Workflow
{
    /// <summary>
    /// Orchestrates a complete automation run: builds the context, executes every enabled
    /// <see cref="IWorkflowStep"/> in order, reports progress, honours cancellation, aggregates
    /// results, and guarantees it never lets an exception escape (the "never crash" requirement).
    /// </summary>
    public interface IWorkflowEngine
    {
        /// <summary>
        /// Runs the full workflow for <paramref name="request"/>.
        /// </summary>
        /// <param name="request">Validated user inputs.</param>
        /// <param name="progress">
        /// Receives a <see cref="WorkflowProgress"/> snapshot before and after each step so the UI can
        /// update its progress bar and status. May be null.
        /// </param>
        /// <param name="cancellationToken">Cancels the run at the next step boundary.</param>
        /// <returns>
        /// A fully populated <see cref="WorkflowResult"/> describing every step's outcome. The task
        /// completes successfully even when steps failed or the run was cancelled — inspect the result.
        /// </returns>
        Task<WorkflowResult> RunAsync(
            WorkflowRequest request,
            IProgress<WorkflowProgress> progress,
            CancellationToken cancellationToken);
    }
}
