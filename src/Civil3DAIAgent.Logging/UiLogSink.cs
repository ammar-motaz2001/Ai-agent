using System;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// A logger that raises an event for each entry so the WPF UI can append it to the on-screen log
    /// window. The UI subscribes to <see cref="EntryLogged"/> and marshals to the dispatcher thread.
    /// This keeps the Logging layer free of any UI/WPF dependency (dependency inversion).
    /// </summary>
    public sealed class UiLogSink : ILogger
    {
        private readonly LogLevel _minimumLevel;

        /// <summary>Creates a UI sink.</summary>
        /// <param name="minimumLevel">Minimum level to surface in the UI.</param>
        public UiLogSink(LogLevel minimumLevel = LogLevel.Information)
        {
            _minimumLevel = minimumLevel;
        }

        /// <summary>Raised (on the logging thread) whenever an entry at or above the minimum level is logged.</summary>
        public event EventHandler<LogEntry> EntryLogged;

        /// <inheritdoc />
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
        {
            if (level < _minimumLevel) return;

            var entry = new LogEntry(DateTime.UtcNow, level, message, category, exception);

            // Defensive: a broken subscriber must not crash the workflow.
            try
            {
                EntryLogged?.Invoke(this, entry);
            }
            catch
            {
                // Swallow: the UI failing to render a log line is never fatal.
            }
        }
    }
}
