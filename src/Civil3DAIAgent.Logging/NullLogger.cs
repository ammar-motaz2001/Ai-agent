using System;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// A logger that discards everything. Useful as a safe default so code never has to null-check a
    /// logger, and as a stand-in in unit tests. Exposes a shared singleton via <see cref="Instance"/>.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        private NullLogger() { }

        /// <summary>Shared, thread-safe, do-nothing instance.</summary>
        public static NullLogger Instance { get; } = new NullLogger();

        /// <inheritdoc />
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
        {
            // Intentionally empty.
        }
    }
}
