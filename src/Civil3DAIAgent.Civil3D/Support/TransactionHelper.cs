using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DAIAgent.Civil3D.Support
{
    /// <summary>
    /// Helpers that run work inside a properly-scoped AutoCAD <see cref="Transaction"/>, committing on
    /// success and always disposing. Centralizing this removes repetitive, error-prone
    /// <c>using</c>/<c>Commit</c> boilerplate from every operation and guarantees no transaction is
    /// ever left open (a common cause of Civil 3D instability).
    /// </summary>
    public static class TransactionHelper
    {
        /// <summary>
        /// Runs <paramref name="body"/> inside a transaction on <paramref name="database"/>, returning
        /// the body's value. The transaction is committed if the body completes without throwing.
        /// </summary>
        public static T InTransaction<T>(Database database, Func<Transaction, T> body)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (body == null) throw new ArgumentNullException(nameof(body));

            using (var tr = database.TransactionManager.StartTransaction())
            {
                var result = body(tr);
                tr.Commit();
                return result;
            }
        }

        /// <summary>Void overload of <see cref="InTransaction{T}"/>.</summary>
        public static void InTransaction(Database database, Action<Transaction> body)
        {
            InTransaction<object>(database, tr => { body(tr); return null; });
        }

        /// <summary>
        /// Runs <paramref name="body"/> while holding a document lock, which is required when modifying
        /// a document from any context that is not already inside a command (e.g. from a modeless UI
        /// or a background continuation). Safe to use even when a lock is already held.
        /// </summary>
        public static T InDocumentLock<T>(Document document, Func<T> body)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (body == null) throw new ArgumentNullException(nameof(body));

            using (document.LockDocument())
            {
                return body();
            }
        }

        /// <summary>Void overload of <see cref="InDocumentLock{T}"/>.</summary>
        public static void InDocumentLock(Document document, Action body)
        {
            InDocumentLock<object>(document, () => { body(); return null; });
        }
    }
}
