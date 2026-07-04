using System;
using System.Collections.Generic;

namespace Civil3DAIAgent.Utilities.Text
{
    /// <summary>
    /// Replaces <c>{token}</c> placeholders in strings. Used to build output file names from patterns
    /// such as <c>"{drawing}_sheets.pdf"</c>. Case-insensitive; unknown tokens are left untouched.
    /// </summary>
    public static class TokenReplacer
    {
        /// <summary>
        /// Replaces each <c>{key}</c> in <paramref name="template"/> with its mapped value.
        /// </summary>
        /// <param name="template">Text containing <c>{token}</c> placeholders.</param>
        /// <param name="tokens">Map of token name (without braces) to replacement value.</param>
        public static string Replace(string template, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(template) || tokens == null) return template ?? string.Empty;

            var result = template;
            foreach (var kvp in tokens)
            {
                result = ReplaceCaseInsensitive(result, "{" + kvp.Key + "}", kvp.Value ?? string.Empty);
            }
            return result;
        }

        /// <summary>Convenience overload for the two common tokens: drawing name and a date string.</summary>
        public static string ReplaceDrawingAndDate(string template, string drawingName, DateTime date)
        {
            return Replace(template, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "drawing", drawingName },
                { "date", date.ToString("yyyyMMdd") }
            });
        }

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            int index = input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                input = input.Substring(0, index) + replacement + input.Substring(index + search.Length);
                index = input.IndexOf(search, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
            }
            return input;
        }
    }
}
