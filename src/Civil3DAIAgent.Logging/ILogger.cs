using System;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// The logging abstraction used everywhere in the system. Implementations decide where entries
    /// go (file, UI window, memory, /dev/null). Kept intentionally small; convenience overloads such
    /// as <c>Info</c>/<c>Warn</c>/<c>Error</c> are provided as extension methods
    /// (see <see cref="LoggerExtensions"/>) so implementers only implement one method.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes a single entry. Implementations must be thread-safe and must never throw
        /// (a logging failure must not crash the workflow).
        /// </summary>
        /// <param name="level">Severity of the entry.</param>
        /// <param name="message">Message text.</param>
        /// <param name="category">Logical source/category (step or class name).</param>
        /// <param name="exception">Optional associated exception.</param>
        void Log(LogLevel level, string message, string category = "", Exception exception = null);
    }
}
