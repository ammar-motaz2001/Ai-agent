using System;
using System.Collections.Generic;
using System.Linq;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// Fans a single log call out to several underlying loggers (composite pattern). This is how the
    /// system writes the same entry to both the log file and the UI window at once. A failure in one
    /// sink never prevents the others from receiving the entry.
    /// </summary>
    public sealed class CompositeLogger : ILogger, IDisposable
    {
        private readonly IReadOnlyList<ILogger> _loggers;

        /// <summary>Creates a composite over the supplied loggers (nulls are ignored).</summary>
        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = (loggers ?? Array.Empty<ILogger>()).Where(l => l != null).ToList();
        }

        /// <summary>Creates a composite over an enumerable of loggers.</summary>
        public CompositeLogger(IEnumerable<ILogger> loggers)
        {
            _loggers = (loggers ?? Enumerable.Empty<ILogger>()).Where(l => l != null).ToList();
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
        {
            foreach (var logger in _loggers)
            {
                try
                {
                    logger.Log(level, message, category, exception);
                }
                catch
                {
                    // One sink failing must not stop the others.
                }
            }
        }

        /// <summary>Disposes any child loggers that are disposable.</summary>
        public void Dispose()
        {
            foreach (var logger in _loggers.OfType<IDisposable>())
            {
                try { logger.Dispose(); } catch { /* ignore */ }
            }
        }
    }
}
