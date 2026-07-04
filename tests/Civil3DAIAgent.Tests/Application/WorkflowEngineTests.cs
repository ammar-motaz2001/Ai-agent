using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Civil3DAIAgent.Application.Workflow;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;
using IConfigProvider = Civil3DAIAgent.Core.Abstractions.IConfigurationProvider;
using Xunit;

namespace Civil3DAIAgent.Tests.Application
{
    public class WorkflowEngineTests
    {
        private static AppSettings Settings(bool continueOnFailure = true)
        {
            var s = new AppSettings { ContinueOnStepFailure = continueOnFailure };
            s.Logging.WriteToFile = false; // keep tests from writing log files
            return s;
        }

        private static WorkflowEngine Build(IEnumerable<IWorkflowStep> steps, AppSettings settings)
        {
            return new WorkflowEngine(
                steps,
                new FakeConfig(settings),
                new FakeValidator(OperationResult.Ok()),
                new FakeExplainer(),
                new RoutingLogger(NullLogger.Instance));
        }

        [Fact]
        public async Task Runs_Steps_In_StepType_Order()
        {
            var order = new List<WorkflowStepType>();
            var steps = new[]
            {
                new FakeStep(WorkflowStepType.CreateAlignment, ctx => { order.Add(WorkflowStepType.CreateAlignment); return OperationResult.Ok(); }),
                new FakeStep(WorkflowStepType.OpenSourceDrawing, ctx => { order.Add(WorkflowStepType.OpenSourceDrawing); return OperationResult.Ok(); }),
            };

            var engine = Build(steps, Settings());
            await engine.RunAsync(new WorkflowRequest(), null, CancellationToken.None);

            Assert.Equal(new[] { WorkflowStepType.OpenSourceDrawing, WorkflowStepType.CreateAlignment }, order);
        }

        [Fact]
        public async Task Skips_Step_When_CanExecute_False()
        {
            bool executed = false;
            var step = new FakeStep(WorkflowStepType.CopyContours, ctx => { executed = true; return OperationResult.Ok(); })
            {
                CanExecuteResult = false
            };

            var result = await Build(new[] { step }, Settings()).RunAsync(new WorkflowRequest(), null, CancellationToken.None);

            Assert.False(executed);
            Assert.Equal(StepStatus.Skipped, result.Steps[0].Status);
        }

        [Fact]
        public async Task Continues_After_Failure_When_Configured()
        {
            bool secondRan = false;
            var steps = new[]
            {
                new FakeStep(WorkflowStepType.OpenSourceDrawing, ctx => OperationResult.Fail("boom")),
                new FakeStep(WorkflowStepType.SelectRoadPolyline, ctx => { secondRan = true; return OperationResult.Ok(); }),
            };

            var result = await Build(steps, Settings(continueOnFailure: true)).RunAsync(new WorkflowRequest(), null, CancellationToken.None);

            Assert.True(secondRan);
            Assert.Equal(StepStatus.Failed, result.Steps[0].Status);
            Assert.Equal(StepStatus.Succeeded, result.Steps[1].Status);
        }

        [Fact]
        public async Task Aborts_After_Failure_When_Not_Configured_To_Continue()
        {
            bool secondRan = false;
            var steps = new[]
            {
                new FakeStep(WorkflowStepType.OpenSourceDrawing, ctx => OperationResult.Fail("boom")),
                new FakeStep(WorkflowStepType.SelectRoadPolyline, ctx => { secondRan = true; return OperationResult.Ok(); }),
            };

            var result = await Build(steps, Settings(continueOnFailure: false)).RunAsync(new WorkflowRequest(), null, CancellationToken.None);

            Assert.False(secondRan);
            Assert.Single(result.Steps);
        }

        [Fact]
        public async Task Exception_In_Step_Is_Recorded_Not_Thrown()
        {
            var step = new FakeStep(WorkflowStepType.CreateCorridor, ctx => throw new InvalidOperationException("kaboom"));

            var result = await Build(new[] { step }, Settings()).RunAsync(new WorkflowRequest(), null, CancellationToken.None);

            Assert.Equal(StepStatus.Failed, result.Steps[0].Status);
            Assert.Contains("kaboom", result.Steps[0].Message);
            Assert.NotNull(result.Steps[0].Exception);
        }

        [Fact]
        public async Task Cancellation_Stops_Run_And_Flags_Result()
        {
            var cts = new CancellationTokenSource();
            var steps = new[]
            {
                new FakeStep(WorkflowStepType.OpenSourceDrawing, ctx => { cts.Cancel(); return OperationResult.Ok(); }),
                new FakeStep(WorkflowStepType.SelectRoadPolyline, ctx => OperationResult.Ok()),
            };

            var result = await Build(steps, Settings()).RunAsync(new WorkflowRequest(), null, cts.Token);

            Assert.True(result.WasCancelled);
            Assert.Equal(StepStatus.Cancelled, result.Steps[1].Status);
        }

        [Fact]
        public async Task Reports_Progress_For_Each_Step()
        {
            var progress = new FakeProgress();
            var steps = new[] { new FakeStep(WorkflowStepType.OpenSourceDrawing, ctx => OperationResult.Ok()) };

            await Build(steps, Settings()).RunAsync(new WorkflowRequest(), progress, CancellationToken.None);

            Assert.NotEmpty(progress.Reports);
        }

        // ---------------- fakes ----------------
        private sealed class FakeStep : WorkflowStepBase
        {
            private readonly Func<IWorkflowContext, OperationResult> _body;
            public FakeStep(WorkflowStepType type, Func<IWorkflowContext, OperationResult> body) { StepType = type; _body = body; }
            public override WorkflowStepType StepType { get; }
            public override string DisplayName => StepType.ToString();
            public bool CanExecuteResult { get; set; } = true;
            public override bool CanExecute(IWorkflowContext context) => CanExecuteResult;
            public override OperationResult Execute(IWorkflowContext context) => _body(context);
        }

        private sealed class FakeConfig : IConfigProvider
        {
            public FakeConfig(AppSettings s) { Settings = s; }
            public AppSettings Settings { get; }
            public AppSettings Reload() => Settings;
        }

        private sealed class FakeValidator : IInputValidator
        {
            private readonly OperationResult _result;
            public FakeValidator(OperationResult result) { _result = result; }
            public OperationResult Validate(WorkflowRequest request) => _result;
        }

        private sealed class FakeExplainer : IExceptionExplainer
        {
            public string Explain(Exception exception) => exception?.Message ?? "";
        }

        private sealed class FakeProgress : IProgress<WorkflowProgress>
        {
            public readonly List<WorkflowProgress> Reports = new List<WorkflowProgress>();
            public void Report(WorkflowProgress value) { lock (Reports) Reports.Add(value); }
        }
    }
}
