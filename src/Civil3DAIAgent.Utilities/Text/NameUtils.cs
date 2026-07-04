using System;
using System.Collections.Generic;
using System.Linq;

namespace Civil3DAIAgent.Utilities.Text
{
    /// <summary>
    /// Naming helpers. Civil 3D rejects duplicate object names (alignment, surface, profile, ...), so
    /// the automation makes names unique before creating objects to avoid <c>eDuplicateRecordName</c>.
    /// </summary>
    public static class NameUtils
    {
        /// <summary>
        /// Returns <paramref name="desired"/> if it is not already in <paramref name="existing"/>;
        /// otherwise appends " (2)", " (3)", ... until the name is unique. Comparison is
        /// case-insensitive to match Civil 3D's own uniqueness rule.
        /// </summary>
        public static string MakeUnique(string desired, IEnumerable<string> existing)
        {
            if (string.IsNullOrWhiteSpace(desired)) desired = "Item";
            var set = new HashSet<string>(
                (existing ?? Enumerable.Empty<string>()).Where(s => s != null),
                StringComparer.OrdinalIgnoreCase);

            if (!set.Contains(desired)) return desired;

            for (int i = 2; i < int.MaxValue; i++)
            {
                var candidate = $"{desired} ({i})";
                if (!set.Contains(candidate)) return candidate;
            }
            return desired + " " + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }
}
