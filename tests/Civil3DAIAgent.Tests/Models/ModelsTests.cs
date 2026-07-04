using System;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Volumes;
using Civil3DAIAgent.Models.Workflow;
using Civil3DAIAgent.Models.Enums;
using Xunit;

namespace Civil3DAIAgent.Tests.Models
{
    public class OperationResultTests
    {
        [Fact]
        public void Ok_IsSucceeded_NoException()
        {
            var r = OperationResult.Ok("done");
            Assert.True(r.Succeeded);
            Assert.False(r.Failed);
            Assert.Equal("done", r.Message);
            Assert.Null(r.Exception);
        }

        [Fact]
        public void Fail_WithException_PreservesIt()
        {
            var ex = new InvalidOperationException("boom");
            var r = OperationResult.Fail("nope", ex);
            Assert.True(r.Failed);
            Assert.Same(ex, r.Exception);
        }

        [Fact]
        public void Ok_WithWarnings_ReportsHasWarnings()
        {
            var r = OperationResult.Ok("ok", new[] { "w1" });
            Assert.True(r.HasWarnings);
            Assert.Single(r.Warnings);
        }

        [Fact]
        public void Generic_Ok_CarriesValue()
        {
            var r = OperationResult<int>.Ok(42);
            Assert.True(r.Succeeded);
            Assert.Equal(42, r.Value);
        }

        [Fact]
        public void Generic_Fail_HasDefaultValue()
        {
            var r = OperationResult<string>.Fail("bad");
            Assert.True(r.Failed);
            Assert.Null(r.Value);
        }

        [Fact]
        public void Warnings_NeverNull()
        {
            Assert.NotNull(OperationResult.Fail("x").Warnings);
        }
    }

    public class VolumeSummaryTests
    {
        [Fact]
        public void Net_IsCutMinusFill()
        {
            var v = new VolumeSummary { CutVolume = 100, FillVolume = 40 };
            Assert.Equal(60, v.NetVolume);
        }

        [Fact]
        public void IsEmpty_WhenBothZero()
        {
            Assert.True(new VolumeSummary().IsEmpty);
        }

        [Fact]
        public void Display_MentionsBorrow_WhenNetNegative()
        {
            var v = new VolumeSummary { CutVolume = 10, FillVolume = 30 };
            Assert.Contains("borrow", v.ToDisplayString());
        }
    }

    public class SurveyPointTests
    {
        [Fact]
        public void IsValid_TrueForFiniteCoordinates()
        {
            Assert.True(new SurveyPoint(1, 100, 200, 5, "EP").IsValid);
        }

        [Fact]
        public void IsValid_FalseForNaN()
        {
            Assert.False(new SurveyPoint(1, double.NaN, 200, 5, "").IsValid);
        }

        [Fact]
        public void Description_NeverNull()
        {
            Assert.Equal(string.Empty, new SurveyPoint(1, 0, 0, 0, null).Description);
        }
    }

    public class WorkflowResultTests
    {
        [Fact]
        public void OverallSuccess_TrueWhenAllStepsSucceed()
        {
            var r = new WorkflowResult();
            r.Steps.Add(new StepResult { Status = StepStatus.Succeeded });
            r.Steps.Add(new StepResult { Status = StepStatus.Skipped });
            Assert.True(r.OverallSuccess);
        }

        [Fact]
        public void OverallSuccess_FalseWhenAnyFailure()
        {
            var r = new WorkflowResult();
            r.Steps.Add(new StepResult { Status = StepStatus.Succeeded });
            r.Steps.Add(new StepResult { Status = StepStatus.Failed });
            Assert.False(r.OverallSuccess);
            Assert.Equal(1, r.FailureCount);
        }

        [Fact]
        public void OverallSuccess_FalseWhenCancelled()
        {
            var r = new WorkflowResult { WasCancelled = true };
            Assert.False(r.OverallSuccess);
        }
    }

    public class WorkflowProgressTests
    {
        [Fact]
        public void PercentComplete_ComputesRatio()
        {
            var p = new WorkflowProgress(5, 20, WorkflowStepType.CreateAlignment, "Create Alignment", StepStatus.Running);
            Assert.Equal(25.0, p.PercentComplete);
        }

        [Fact]
        public void PercentComplete_ZeroWhenNoSteps()
        {
            var p = new WorkflowProgress(0, 0, WorkflowStepType.OpenSourceDrawing, "x", StepStatus.Pending);
            Assert.Equal(0.0, p.PercentComplete);
        }
    }
}
