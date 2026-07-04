using System;
using System.Threading;
using System.Threading.Tasks;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Services.Facade
{
    /// <summary>
    /// The single high-level entry point the UI (and command line) use to drive the automation. Hides
    /// the engine, configuration, validation, and logging wiring behind one small surface.
    /// </summary>
    public interface IAutomationService
    {
        /// <summary>The UI log sink to subscribe to for live log entries.</summary>
        UiLogSink LogSink { get; }

        /// <summary>The current, effective application settings.</summary>
        AppSettings Settings { get; }

        /// <summary>Reloads settings from <c>appsettings.json</c> and returns them.</summary>
        AppSettings ReloadSettings();

        /// <summary>Validates the request, returning a friendly aggregated result.</summary>
        OperationResult ValidateInputs(WorkflowRequest request);

        /// <summary>
        /// Runs the full 23-step workflow. Never throws; inspect the returned
        /// <see cref="WorkflowResult"/> for per-step outcomes.
        /// </summary>
        Task<WorkflowResult> RunAsync(WorkflowRequest request, IProgress<WorkflowProgress> progress, CancellationToken cancellationToken);
    }
}
