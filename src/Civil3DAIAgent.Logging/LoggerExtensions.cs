using System;
using System.Diagnostics;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// Convenience helpers layered on top of the single-method <see cref="ILogger"/> interface.
    /// Keeps the interface minimal (one method to implement) while giving callers ergonomic
    /// level-specific methods and an operation-timing helper.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>Logs at <see cref="LogLevel.Trace"/>.</summary>
        public static void Trace(this ILogger logger, string message, string category = "")
            => logger?.Log(LogLevel.Trace, message, category);

        /// <summary>Logs at <see cref="LogLevel.Debug"/>.</summary>
        public static void Debug(this ILogger logger, string message, string category = "")
            => logger?.Log(LogLevel.Debug, message, category);

        /// <summary>Logs at <see cref="LogLevel.Information"/>.</summary>
        public static void Info(this ILogger logger, string message, string category = "")
            => logger?.Log(LogLevel.Information, message, category);

        /// <summary>Logs at <see cref="LogLevel.Warning"/>.</summary>
        public static void Warn(this ILogger logger, string message, string category = "")
            => logger?.Log(LogLevel.Warning, message, category);

        /// <summary>Logs at <see cref="LogLevel.Error"/>, optionally with an exception.</summary>
        public static void Error(this ILogger logger, string message, Exception ex = null, string category = "")
            => logger?.Log(LogLevel.Error, message, category, ex);

        /// <summary>Logs at <see cref="LogLevel.Critical"/>, optionally with an exception.</summary>
        public static void Critical(this ILogger logger, string message, Exception ex = null, string category = "")
            => logger?.Log(LogLevel.Critical, message, category, ex);

        /// <summary>
        /// Starts a disposable timer that logs the elapsed time when disposed. Use with <c>using</c>:
        /// <code>using (logger.TimeOperation("Create Alignment")) { ... }</code>
        /// Produces "Create Alignment finished in 1.23s" on dispose.
        /// </summary>
        public static IDisposable TimeOperation(this ILogger logger, string operationName, string category = "")
            => new OperationTimer(logger, operationName, category);

        /// <summary>Disposable that measures and logs elapsed time. Never throws on dispose.</summary>
        private sealed class OperationTimer : IDisposable
        {
            private readonly ILogger _logger;
            private readonly string _operationName;
            private readonly string _category;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public OperationTimer(ILogger logger, string operationName, string category)
            {
                _logger = logger;
                _operationName = operationName ?? "operation";
                _category = category;
                _stopwatch = Stopwatch.StartNew();
                _logger.Debug(_operationName + " started...", _category);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    _stopwatch.Stop();
                    _logger.Info(
                        string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0} finished in {1:F2}s", _operationName, _stopwatch.Elapsed.TotalSeconds),
                        _category);
                }
                catch
                {
                    // Logging must never throw from a using-block dispose.
                }
            }
        }
    }
}
