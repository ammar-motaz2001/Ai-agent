namespace Civil3DAIAgent.Models.Enums
{
    /// <summary>
    /// Severity of a log entry, ordered from most verbose (<see cref="Trace"/>) to most severe
    /// (<see cref="Critical"/>). A logger with a configured minimum level suppresses everything below it.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Extremely detailed diagnostic output (API call arguments, ids). Off by default.</summary>
        Trace = 0,

        /// <summary>Developer-oriented diagnostic information.</summary>
        Debug = 1,

        /// <summary>Normal progress messages ("Alignment created", "Surface built").</summary>
        Information = 2,

        /// <summary>Something unexpected but recoverable (a step continued with defaults).</summary>
        Warning = 3,

        /// <summary>An operation failed but the workflow can continue or be recovered.</summary>
        Error = 4,

        /// <summary>A fatal condition that aborts the entire run.</summary>
        Critical = 5
    }
}
