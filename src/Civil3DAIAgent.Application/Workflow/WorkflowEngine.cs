using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;

namespace Civil3DAIAgent.Application.Workflow
{
    /// <summary>
    /// Default <see cref="IWorkflowEngine"/>. Validates inputs, builds the run context and a per-run
    /// file logger, then executes every enabled <see cref="IWorkflowStep"/> in <see cref="WorkflowStepType"/>
    /// order. Each step runs inside its own try/catch and timing scope so that:
    /// <list type="bullet">
    /// <item>a single step failure never crashes the process (the "never crash" requirement);</item>
    /// <item>every exception is explained in plain language via <see cref="IExceptionExplainer"/>;</item>
    /// <item>execution time, warnings, and outcome are recorded per step;</item>
    /// <item>the run continues or aborts per <see cref="AppSettings.ContinueOnStepFailure"/>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// THREADING: the Civil 3D API has hard affinity to AutoCAD's main (document) thread. The engine
    /// therefore runs the whole workflow SYNCHRONOUSLY on the caller's thread and never introduces an
    /// await that could resume on a thread-pool thread. The UI calls <see cref="RunAsync"/> from the
    /// modeless window on the main thread (application context), and steps acquire document locks via
    /// the Civil3D <c>TransactionHelper</c> before modifying a drawing.
    /// </remarks>
    public sealed class WorkflowEngine : IWorkflowEngine
    {
        private const string Category = "Engine";

        private readonly IReadOnlyList<IWorkflowStep> _steps;
        private readonly IConfigurationProvider _configuration;
        private readonly IInputValidator _validator;
        private readonly IExceptionExplainer _explainer;
        private readonly RoutingLogger _logger;

        /// <summary>Creates the engine.</summary>
        /// <param name="steps">All registered steps (any order; the engine sorts them).</param>
        /// <param name="configuration">Provides <see cref="AppSettings"/>.</param>
        /// <param name="validator">Validates the request before running.</param>
        /// <param name="explainer">Turns exceptions into plain-language messages.</param>
        /// <param name="logger">
        /// The shared routing logger. It always writes to the UI sink; the engine attaches a per-run
        /// file logger to it so services' and steps' logs are all captured in the run's log file.
        /// </param>
        public WorkflowEngine(
            IEnumerable<IWorkflowStep> steps,
            IConfigurationProvider configuration,
            IInputValidator validator,
            IExceptionExplainer explainer,
            RoutingLogger logger)
        {
            _steps = (steps ?? Enumerable.Empty<IWorkflowStep>())
                .OrderBy(s => (int)s.StepType)
                .ToList();
            _configuration = configuration;
            _validator = validator;
            _explainer = explainer;
            _logger = logger ?? new RoutingLogger(NullLogger.Instance);
        }

        /// <inheritdoc />
        public Task<WorkflowResult> RunAsync(
            WorkflowRequest request, IProgress<WorkflowProgress> progress, CancellationToken cancellationToken)
        {
            // CRITICAL: run fully synchronously and return an already-completed task. The Civil 3D API
            // has hard thread affinity to the main document thread; introducing any await that resumes
            // on a thread-pool thread (e.g. Task.Yield / Task.Run) makes the next API call crash the
            // process with an access violation. The caller invokes this on the main thread, so all API
            // calls stay on the correct thread. The UI trades some responsiveness for correctness.
            try
            {
                return Task.FromResult(RunSynchronously(request, progress, cancellationToken));
            }
            catch (Exception ex)
            {
                // Never let the engine throw to the UI/host.
                _logger.Critical("Engine failed before completing: " + (_explainer?.Explain(ex) ?? ex.Message), ex, Category);
                var fallback = new WorkflowResult { StartedUtc = DateTime.UtcNow, FinishedUtc = DateTime.UtcNow };
                return Task.FromResult(fallback);
            }
        }

        /// <summary>Synchronous run implementation. Executes entirely on the caller's (document) thread.</summary>
        private WorkflowResult RunSynchronously(WorkflowRequest request, IProgress<WorkflowProgress> progress, CancellationToken ct)
        {
            var result = new WorkflowResult { StartedUtc = DateTime.UtcNow };
            var settings = _configuration?.Settings ?? new AppSettings();

            // Attach a per-run file logger to the routing logger so this run gets its own timestamped
            // log file capturing service, step, and engine output alike.
            ILogger runLogger = _logger;
            FileLogger fileLogger = TryCreateFileLogger(request, settings);
            if (fileLogger != null) _logger.AttachPerRun(fileLogger);

            try
            {
                runLogger.Info("=== Civil3D AI Agent run started ===", Category);
                runLogger.Info($"Source DWG: {request?.InputDwgPath}", Category);
                runLogger.Info($"Output folder: {request?.OutputFolder}", Category);

                // Validate up front so problems surface immediately.
                var validation = _validator?.Validate(request) ?? OperationResult.Ok();
                foreach (var w in validation.Warnings) runLogger.Warn(w, Category);
                if (validation.Failed)
                {
                    runLogger.Critical("Input validation failed: " + validation.Message, null, Category);
                    result.FinishedUtc = DateTime.UtcNow;
                    return result; // No steps run; OverallSuccess will be true only if there are no steps — treat as failure via message
                }

                using (var context = new WorkflowContext(request, settings, runLogger, ct))
                {
                    int total = _steps.Count;
                    int completed = 0;

                    foreach (var step in _steps)
                    {
                        runLogger.Info($"──► BEGIN step {(int)step.StepType}/23: {step.DisplayName}", Category);
                        ReportProgress(progress, completed, total, step, StepStatus.Running);

                        var stepResult = ExecuteStep(step, context, runLogger);
                        result.Steps.Add(stepResult);
                        completed++;
                        runLogger.Info($"──◄ END   step {(int)step.StepType}/23: {step.DisplayName} → {stepResult.Status} " +
                                       $"({stepResult.Duration.TotalSeconds:F2}s)", Category);

                        ReportProgress(progress, completed, total, step, stepResult.Status);

                        if (stepResult.Status == StepStatus.Cancelled)
                        {
                            result.WasCancelled = true;
                            runLogger.Warn("Run cancelled by user.", Category);
                            break;
                        }

                        if (stepResult.Status == StepStatus.Failed && !settings.ContinueOnStepFailure)
                        {
                            runLogger.Critical("Aborting run because a step failed and ContinueOnStepFailure is false.", null, Category);
                            break;
                        }
                    }

                    // Harvest output artefacts recorded by the steps.
                    HarvestOutputs(context, result);
                }

                result.FinishedUtc = DateTime.UtcNow;
                LogSummary(result, runLogger);
                return result;
            }
            catch (Exception ex)
            {
                // Absolute backstop — the engine itself must never throw to the caller.
                runLogger.Critical("Unexpected engine failure: " + (_explainer?.Explain(ex) ?? ex.Message), ex, Category);
                result.FinishedUtc = DateTime.UtcNow;
                return result;
            }
            finally
            {
                _logger.DetachPerRun();
                fileLogger?.Dispose();
            }
        }

        /// <summary>Executes one step with timing, cancellation, skip handling, and exception recovery.</summary>
        private StepResult ExecuteStep(IWorkflowStep step, IWorkflowContext context, ILogger logger)
        {
            var stepResult = new StepResult
            {
                Step = step.StepType,
                DisplayName = step.DisplayName,
                Status = StepStatus.Running,
                StartedUtc = DateTime.UtcNow
            };

            var stopwatch = Stopwatch.StartNew();
            logger.Info($"▶ {step.DisplayName} ...", Category);

            try
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    stepResult.Status = StepStatus.Cancelled;
                    stepResult.Message = "Cancelled before the step started.";
                    return stepResult;
                }

                if (!step.CanExecute(context))
                {
                    stepResult.Status = StepStatus.Skipped;
                    stepResult.Message = "Skipped (prerequisite not met or optional input absent).";
                    logger.Info($"⏭ {step.DisplayName}: skipped.", Category);
                    return stepResult;
                }

                OperationResult result = step.Execute(context) ?? OperationResult.Fail("The step returned no result.");

                if (result.Succeeded)
                {
                    stepResult.Status = result.HasWarnings ? StepStatus.CompletedWithWarnings : StepStatus.Succeeded;
                    stepResult.Message = result.Message;
                    foreach (var w in result.Warnings)
                    {
                        stepResult.Warnings.Add(w);
                        logger.Warn("  ⚠ " + w, Category);
                    }
                }
                else
                {
                    stepResult.Status = StepStatus.Failed;
                    stepResult.Message = result.Message;
                    stepResult.Exception = result.Exception;
                    logger.Error("  ✖ " + step.DisplayName + " failed: " + result.Message, result.Exception, Category);
                }
            }
            catch (OperationCanceledException)
            {
                stepResult.Status = StepStatus.Cancelled;
                stepResult.Message = "Cancelled during execution.";
            }
            catch (Exception ex)
            {
                stepResult.Status = StepStatus.Failed;
                stepResult.Message = _explainer?.Explain(ex) ?? ex.Message;
                stepResult.Exception = ex;
                logger.Error("  ✖ " + step.DisplayName + " threw: " + stepResult.Message, ex, Category);
            }
            finally
            {
                stopwatch.Stop();
                stepResult.FinishedUtc = DateTime.UtcNow;
                if (stepResult.IsSuccessful)
                    logger.Info($"✔ {step.DisplayName} ({stepResult.Duration.TotalSeconds:F2}s)", Category);
            }

            return stepResult;
        }

        /// <summary>Copies output paths recorded by steps into the result.</summary>
        private static void HarvestOutputs(IWorkflowContext context, WorkflowResult result)
        {
            if (context.TryGet<string>(WorkflowContextKeys.SavedDrawingPath, out var dwg) && !string.IsNullOrEmpty(dwg))
                result.OutputDwgPath = dwg;
            else if (context.TryGet<string>(WorkflowContextKeys.NewDrawingPath, out var newDwg))
                result.OutputDwgPath = newDwg;

            if (context.TryGet<IReadOnlyList<string>>(WorkflowContextKeys.PdfOutputPaths, out var pdfs) && pdfs != null)
                result.OutputPdfPaths.AddRange(pdfs);
        }

        /// <summary>Builds a per-run file logger under &lt;output&gt;\logs (or configured folder). Never throws.</summary>
        private FileLogger TryCreateFileLogger(WorkflowRequest request, AppSettings settings)
        {
            try
            {
                if (!settings.Logging.WriteToFile) return null;

                string folder = !string.IsNullOrWhiteSpace(settings.Logging.LogFolder)
                    ? settings.Logging.LogFolder
                    : Path.Combine(request?.OutputFolder ?? ".", "logs");

                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "run_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                return new FileLogger(file, settings.Logging.MinimumLevel);
            }
            catch
            {
                return null; // logging must never block the run
            }
        }

        private static void ReportProgress(IProgress<WorkflowProgress> progress, int completed, int total,
            IWorkflowStep step, StepStatus status)
        {
            progress?.Report(new WorkflowProgress(completed, total, step.StepType, step.DisplayName, status));
        }

        /// <summary>Writes a final roll-up to the log.</summary>
        private static void LogSummary(WorkflowResult result, ILogger logger)
        {
            logger.Info("=== Run finished in " + result.TotalDuration.TotalSeconds.ToString("F1") + "s ===", Category);
            logger.Info($"Steps: {result.Steps.Count}, failures: {result.FailureCount}, warnings: {result.WarningCount}, " +
                        $"cancelled: {result.WasCancelled}", Category);
            if (!string.IsNullOrEmpty(result.OutputDwgPath)) logger.Info("Output DWG: " + result.OutputDwgPath, Category);
            foreach (var pdf in result.OutputPdfPaths) logger.Info("Output PDF: " + pdf, Category);
        }
    }
}
