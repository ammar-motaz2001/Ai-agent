using System;
using System.Threading;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Workflow;
using Xunit;

namespace Civil3DAIAgent.Tests.Core
{
    public class WorkflowContextTests
    {
        private static WorkflowContext NewContext() =>
            new WorkflowContext(new WorkflowRequest(), new AppSettings(), NullLogger.Instance, CancellationToken.None);

        [Fact]
        public void Set_Then_TryGet_ReturnsValue()
        {
            var ctx = NewContext();
            ctx.Set("k", "v");
            Assert.True(ctx.TryGet<string>("k", out var value));
            Assert.Equal("v", value);
        }

        [Fact]
        public void TryGet_WrongType_ReturnsFalse()
        {
            var ctx = NewContext();
            ctx.Set("k", "v");
            Assert.False(ctx.TryGet<int>("k", out _));
        }

        [Fact]
        public void Get_Missing_Throws()
        {
            var ctx = NewContext();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => ctx.Get<string>("missing"));
        }

        [Fact]
        public void Contains_ReflectsPresence()
        {
            var ctx = NewContext();
            Assert.False(ctx.Contains("k"));
            ctx.Set("k", 1);
            Assert.True(ctx.Contains("k"));
        }

        [Fact]
        public void Dispose_DisposesRegisteredResources_InReverseOrder()
        {
            var ctx = NewContext();
            var order = new System.Collections.Generic.List<int>();
            ctx.RegisterForDisposal(new ActionDisposable(() => order.Add(1)));
            ctx.RegisterForDisposal(new ActionDisposable(() => order.Add(2)));

            ctx.Dispose();

            Assert.Equal(new[] { 2, 1 }, order);
        }

        [Fact]
        public void Dispose_SwallowsDisposalErrors()
        {
            var ctx = NewContext();
            ctx.RegisterForDisposal(new ActionDisposable(() => throw new InvalidOperationException()));
            var ex = Record.Exception(() => ctx.Dispose());
            Assert.Null(ex);
        }

        private sealed class ActionDisposable : IDisposable
        {
            private readonly Action _onDispose;
            public ActionDisposable(Action onDispose) { _onDispose = onDispose; }
            public void Dispose() => _onDispose();
        }
    }
}
