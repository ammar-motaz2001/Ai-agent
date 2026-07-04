using System;

namespace Civil3DAIAgent.Utilities
{
    /// <summary>
    /// Small argument-guard helpers to keep constructors and methods readable while still failing
    /// fast with clear messages on programmer errors.
    /// </summary>
    public static class Guard
    {
        /// <summary>Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is null; otherwise returns it.</summary>
        public static T NotNull<T>(T value, string paramName) where T : class
        {
            if (value == null) throw new ArgumentNullException(paramName);
            return value;
        }

        /// <summary>Throws <see cref="ArgumentException"/> when the string is null/empty; otherwise returns it.</summary>
        public static string NotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value must not be null or empty.", paramName);
            return value;
        }

        /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> when <paramref name="value"/> is not &gt; 0.</summary>
        public static double Positive(double value, string paramName)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be a positive, finite number.");
            return value;
        }
    }
}
