using System;
using System.IO;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Enums;
using Xunit;

namespace Civil3DAIAgent.Tests.Logging
{
    public class LogEntryTests
    {
        [Fact]
        public void ToLogLine_ContainsLevelCategoryMessage()
        {
            var e = new LogEntry(DateTime.UtcNow, LogLevel.Warning, "hello", "Cat", null);
            var line = e.ToLogLine();
            Assert.Contains("WARN", line);
            Assert.Contains("[Cat]", line);
            Assert.Contains("hello", line);
        }

        [Fact]
        public void ToLogLine_IncludesExceptionDetails()
        {
            var e = new LogEntry(DateTime.UtcNow, LogLevel.Error, "failed", "Cat", new InvalidOperationException("boom"));
            var line = e.ToLogLine();
            Assert.Contains("InvalidOperationException", line);
            Assert.Contains("boom", line);
        }
    }

    public class CompositeLoggerTests
    {
        [Fact]
        public void Log_FansOutToAllChildren()
        {
            var a = new CountingLogger();
            var b = new CountingLogger();
            var composite = new CompositeLogger(a, b);
            composite.Info("x");
            Assert.Equal(1, a.Count);
            Assert.Equal(1, b.Count);
        }

        [Fact]
        public void Log_ContinuesWhenOneChildThrows()
        {
            var throwing = new ThrowingLogger();
            var ok = new CountingLogger();
            var composite = new CompositeLogger(throwing, ok);
            composite.Info("x");
            Assert.Equal(1, ok.Count); // second logger still received it
        }
    }

    public class RoutingLoggerTests
    {
        [Fact]
        public void Log_AlwaysHitsPermanent()
        {
            var permanent = new CountingLogger();
            var routing = new RoutingLogger(permanent);
            routing.Info("x");
            Assert.Equal(1, permanent.Count);
        }

        [Fact]
        public void Log_HitsPerRun_OnlyWhileAttached()
        {
            var permanent = new CountingLogger();
            var perRun = new CountingLogger();
            var routing = new RoutingLogger(permanent);

            routing.Info("before");            // per-run not attached
            routing.AttachPerRun(perRun);
            routing.Info("during");            // both
            routing.DetachPerRun();
            routing.Info("after");             // per-run not attached

            Assert.Equal(3, permanent.Count);
            Assert.Equal(1, perRun.Count);
        }
    }

    public class FileLoggerTests
    {
        [Fact]
        public void Writes_And_RespectsMinimumLevel()
        {
            string path = Path.Combine(Path.GetTempPath(), "c3dai_test_" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                using (var logger = new FileLogger(path, LogLevel.Information))
                {
                    logger.Debug("should-be-filtered");
                    logger.Info("should-appear");
                }

                var text = File.ReadAllText(path);
                Assert.Contains("should-appear", text);
                Assert.DoesNotContain("should-be-filtered", text);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    // ---- test doubles ----
    internal sealed class CountingLogger : ILogger
    {
        public int Count;
        public void Log(LogLevel level, string message, string category = "", Exception exception = null) => Count++;
    }

    internal sealed class ThrowingLogger : ILogger
    {
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
            => throw new InvalidOperationException("sink failure");
    }
}
