using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DAIAgent.Civil3D.Support
{
    /// <summary>
    /// Converts between AutoCAD persistent handle strings (the neutral identifiers passed between
    /// workflow steps) and live <see cref="ObjectId"/>s within a database. Using handles as the
    /// currency between steps keeps the Application/orchestration layer free of Autodesk types.
    /// </summary>
    public static class HandleUtils
    {
        /// <summary>
        /// Tries to resolve a hexadecimal handle string to an <see cref="ObjectId"/> in
        /// <paramref name="db"/>. Returns false for null/blank/invalid handles.
        /// </summary>
        public static bool TryResolve(Database db, string handleString, out ObjectId id)
        {
            id = ObjectId.Null;
            if (db == null || string.IsNullOrWhiteSpace(handleString)) return false;
            try
            {
                long value = Convert.ToInt64(handleString, 16);
                id = db.GetObjectId(false, new Handle(value), 0);
                return IsUsable(id);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True when an <see cref="ObjectId"/> is safe to open: non-null, valid, and not erased. Always
        /// check this before calling <c>Transaction.GetObject</c> — opening a null/erased id is a common
        /// cause of hard (unmanaged) Civil 3D crashes.
        /// </summary>
        public static bool IsUsable(ObjectId id) => !id.IsNull && id.IsValid && !id.IsErased;

        /// <summary>
        /// Resolves a handle string to an <see cref="ObjectId"/>, throwing
        /// <see cref="InvalidOperationException"/> when it cannot be resolved. Use when the handle is
        /// guaranteed to exist (produced by a preceding step in the same drawing).
        /// </summary>
        public static ObjectId Resolve(Database db, string handleString)
        {
            if (TryResolve(db, handleString, out var id)) return id;
            throw new InvalidOperationException(
                $"The object with handle '{handleString}' could not be found in the drawing. " +
                "A preceding step may have failed to create it.");
        }
    }
}
