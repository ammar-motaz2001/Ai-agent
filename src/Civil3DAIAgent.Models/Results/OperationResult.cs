using System;
using System.Collections.Generic;

namespace Civil3DAIAgent.Models.Results
{
    /// <summary>
    /// A lightweight, allocation-friendly result type used throughout the system instead of
    /// throwing for expected/handleable failures. Encourages the "never crash, recover where
    /// possible" policy: callers inspect <see cref="Succeeded"/> rather than wrapping every call
    /// in try/catch. Genuinely exceptional conditions still throw and are caught at the step boundary.
    /// </summary>
    public class OperationResult
    {
        /// <summary>Protected constructor; use the factory methods (<see cref="Ok"/>, <see cref="Fail(string)"/>).</summary>
        protected OperationResult(bool succeeded, string message, Exception exception, IReadOnlyList<string> warnings)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Exception = exception;
            Warnings = warnings ?? Array.Empty<string>();
        }

        /// <summary>True when the operation completed without a fatal error.</summary>
        public bool Succeeded { get; }

        /// <summary>True when the operation did not complete successfully.</summary>
        public bool Failed => !Succeeded;

        /// <summary>Human-readable summary (success note or failure explanation). Never null.</summary>
        public string Message { get; }

        /// <summary>The underlying exception when the failure originated from one; otherwise null.</summary>
        public Exception Exception { get; }

        /// <summary>Non-fatal warnings collected while producing this result. Never null.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>True when the operation succeeded but produced one or more warnings.</summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>Creates a successful result.</summary>
        public static OperationResult Ok(string message = "", IReadOnlyList<string> warnings = null) =>
            new OperationResult(true, message, null, warnings);

        /// <summary>Creates a failed result from a message.</summary>
        public static OperationResult Fail(string message) =>
            new OperationResult(false, message, null, null);

        /// <summary>Creates a failed result from an exception, preserving the original for logging.</summary>
        public static OperationResult Fail(string message, Exception exception) =>
            new OperationResult(false, message, exception, null);
    }

    /// <summary>
    /// A strongly-typed result that also carries a value on success. Use for operations that produce
    /// an object (e.g. a created Alignment id, a parsed point list).
    /// </summary>
    /// <typeparam name="T">Type of the value produced on success.</typeparam>
    public sealed class OperationResult<T> : OperationResult
    {
        private OperationResult(bool succeeded, T value, string message, Exception exception, IReadOnlyList<string> warnings)
            : base(succeeded, message, exception, warnings)
        {
            Value = value;
        }

        /// <summary>The produced value. Meaningful only when <see cref="OperationResult.Succeeded"/> is true.</summary>
        public T Value { get; }

        /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
        public static OperationResult<T> Ok(T value, string message = "", IReadOnlyList<string> warnings = null) =>
            new OperationResult<T>(true, value, message, null, warnings);

        /// <summary>Creates a failed typed result from a message.</summary>
        public static new OperationResult<T> Fail(string message) =>
            new OperationResult<T>(false, default(T), message, null, null);

        /// <summary>Creates a failed typed result from an exception.</summary>
        public static new OperationResult<T> Fail(string message, Exception exception) =>
            new OperationResult<T>(false, default(T), message, exception, null);
    }
}
