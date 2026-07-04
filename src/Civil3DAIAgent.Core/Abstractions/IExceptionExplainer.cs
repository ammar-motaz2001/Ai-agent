using System;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// Translates raw exceptions (especially Autodesk <c>ErrorStatus</c>-carrying exceptions) into a
    /// clear, non-technical explanation plus a suggested remedy. Implemented in the Civil3D layer,
    /// where the Autodesk exception types are available. This satisfies the requirement to "explain
    /// every Civil 3D exception".
    /// </summary>
    public interface IExceptionExplainer
    {
        /// <summary>
        /// Returns a friendly, actionable explanation for <paramref name="exception"/>. For AutoCAD
        /// runtime exceptions this decodes the <c>ErrorStatus</c> (e.g. "eKeyNotFound") into words such
        /// as "The named style was not found in the drawing template." Falls back to the exception
        /// message for unknown types. Never returns null.
        /// </summary>
        string Explain(Exception exception);
    }
}
