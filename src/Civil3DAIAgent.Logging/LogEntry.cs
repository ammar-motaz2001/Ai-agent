using System;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// An immutable single log record. Carries everything needed to render the message in the UI,
    /// write it to a file, and correlate it with a workflow step.
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>Creates a log entry.</summary>
        /// <param name="timestampUtc">When the entry was created (UTC).</param>
        /// <param name="level">Severity.</param>
        /// <param name="message">The (already-formatted) message text.</param>
        /// <param name="category">Logical source, typically the step or class name.</param>
        /// <param name="exception">Optional associated exception.</param>
        public LogEntry(DateTime timestampUtc, LogLevel level, string message, string category, Exception exception)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Message = message ?? string.Empty;
            Category = category ?? string.Empty;
            Exception = exception;
        }

        /// <summary>UTC creation time.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>Severity level.</summary>
        public LogLevel Level { get; }

        /// <summary>Formatted message text.</summary>
        public string Message { get; }

        /// <summary>Logical category / source of the entry.</summary>
        public string Category { get; }

        /// <summary>Associated exception, if any.</summary>
        public Exception Exception { get; }

        /// <summary>
        /// Renders the entry as a single log line:
        /// <c>2026-07-04 11:25:03.123 [INFO ] [Alignment] Message text</c>.
        /// </summary>
        public string ToLogLine()
        {
            var local = TimestampUtc.ToLocalTime();
            var levelText = LevelText(Level);
            var line = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [{2}] {3}",
                local, levelText, Category, Message);

            if (Exception != null)
            {
                line += Environment.NewLine + "    " +
                        Exception.GetType().Name + ": " + Exception.Message +
                        Environment.NewLine + Indent(Exception.StackTrace);
            }

            return line;
        }

        /// <summary>Fixed-width, right-padded level tag for column alignment in logs.</summary>
        private static string LevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace: return "TRACE";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Information: return "INFO ";
                case LogLevel.Warning: return "WARN ";
                case LogLevel.Error: return "ERROR";
                case LogLevel.Critical: return "CRIT ";
                default: return "?????";
            }
        }

        private static string Indent(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return "        " + text.Replace(Environment.NewLine, Environment.NewLine + "        ");
        }
    }
}
