using System;
using System.Threading;
using System.Threading.Tasks;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;
using IConfigurationProvider = Civil3DAIAgent.Core.Abstractions.IConfigurationProvider;

namespace Civil3DAIAgent.Services.Facade
{
    /// <summary>Default <see cref="IAutomationService"/> facade over the engine, config, and validator.</summary>
    public sealed class AutomationService : IAutomationService
    {
        private readonly IWorkflowEngine _engine;
        private readonly IConfigurationProvider _configuration;
        private readonly IInputValidator _validator;
        private readonly UiLogSink _logSink;

        /// <summary>Creates the facade.</summary>
        public AutomationService(
            IWorkflowEngine engine,
            IConfigurationProvider configuration,
            IInputValidator validator,
            UiLogSink logSink)
        {
            _engine = engine;
            _configuration = configuration;
            _validator = validator;
            _logSink = logSink;
        }

        /// <inheritdoc />
        public UiLogSink LogSink => _logSink;

        /// <inheritdoc />
        public AppSettings Settings => _configuration.Settings;

        /// <inheritdoc />
        public AppSettings ReloadSettings() => _configuration.Reload();

        /// <inheritdoc />
        public OperationResult ValidateInputs(WorkflowRequest request) => _validator.Validate(request);

        /// <inheritdoc />
        public Task<WorkflowResult> RunAsync(
            WorkflowRequest request, IProgress<WorkflowProgress> progress, CancellationToken cancellationToken)
        {
            return _engine.RunAsync(request, progress, cancellationToken);
        }
    }
}
