using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Civil3DAIAgent.Logging;

namespace Civil3DAIAgent.Civil3D.Support
{
    /// <summary>
    /// Resolves Civil 3D styles (alignment styles, surface styles, label sets, ...) by name with a
    /// graceful fallback. Missing or mistyped style names are the single most common cause of
    /// automation failures, so instead of throwing, this resolver logs a warning and falls back to the
    /// first available style in the collection — keeping the run alive per the "recover where possible"
    /// requirement.
    /// </summary>
    public static class StyleResolver
    {
        /// <summary>
        /// Returns the <see cref="ObjectId"/> of the style named <paramref name="name"/> in
        /// <paramref name="collection"/>. If the name is not found, falls back to the first style in the
        /// collection and logs a warning. Returns <see cref="ObjectId.Null"/> only when the collection is
        /// empty (in which case the caller must handle the missing style explicitly).
        /// </summary>
        /// <param name="collection">The Civil 3D style collection to search.</param>
        /// <param name="name">The desired style name.</param>
        /// <param name="logger">Logger for the fallback warning.</param>
        /// <param name="kind">Human-readable style kind for the warning message (e.g. "alignment style").</param>
        public static ObjectId Resolve(StyleCollectionBase collection, string name, ILogger logger, string kind)
        {
            if (collection == null || collection.Count == 0)
            {
                logger.Warn($"No {kind} exists in the drawing template; the default will be used by Civil 3D.", "Style");
                return ObjectId.Null;
            }

            if (!string.IsNullOrWhiteSpace(name) && collection.Contains(name))
            {
                return collection[name];
            }

            var fallback = collection[0];
            logger.Warn(
                $"The {kind} '{name}' was not found in the template. Falling back to the first available " +
                $"{kind}. Add the style to your template to remove this warning.", "Style");
            return fallback;
        }
    }
}
