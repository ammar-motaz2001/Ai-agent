using System;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using Civil3DAIAgent.Core.Abstractions;

namespace Civil3DAIAgent.Civil3D.Diagnostics
{
    /// <summary>
    /// Implements <see cref="IExceptionExplainer"/> for the Civil 3D / AutoCAD world. AutoCAD wraps
    /// almost every native failure in <see cref="Autodesk.AutoCAD.Runtime.Exception"/> carrying an
    /// <see cref="ErrorStatus"/> enum whose names (e.g. <c>eKeyNotFound</c>) are cryptic to end users.
    /// This class decodes the most common statuses into a plain-language cause + suggested fix so the
    /// log and UI never show a bare "eXXX" code.
    /// </summary>
    public sealed class Civil3DExceptionExplainer : IExceptionExplainer
    {
        /// <inheritdoc />
        public string Explain(Exception exception)
        {
            if (exception == null) return "An unknown error occurred (no exception details available).";

            var sb = new StringBuilder();

            if (exception is Autodesk.AutoCAD.Runtime.Exception acadEx)
            {
                sb.Append(DescribeStatus(acadEx.ErrorStatus));
                sb.Append(" [AutoCAD status: ").Append(acadEx.ErrorStatus).Append("]");
            }
            else
            {
                // Non-AutoCAD exception: lead with the framework message.
                sb.Append(exception.Message);
            }

            // Append inner-exception context when present (often the real cause).
            if (exception.InnerException != null)
            {
                sb.Append(" Inner cause: ").Append(exception.InnerException.Message);
            }

            return sb.ToString();
        }

        /// <summary>Maps a well-known <see cref="ErrorStatus"/> to a friendly explanation + remedy.</summary>
        private static string DescribeStatus(ErrorStatus status)
        {
            switch (status)
            {
                case ErrorStatus.KeyNotFound:
                    return "A required named item (style, layer, page setup, or definition) was not " +
                           "found. Check that your drawing template contains the styles referenced in " +
                           "appsettings.json.";
                case ErrorStatus.DuplicateKey:
                case ErrorStatus.DuplicateRecordName:
                    return "An item with the same name already exists. The automation tried to create " +
                           "something that is already present (e.g. an alignment or surface of that name).";
                case ErrorStatus.WrongObjectType:
                    return "An object was not of the expected type (for example, the selected entity was " +
                           "not a polyline). Verify the selection or the source drawing contents.";
                case ErrorStatus.NullObjectId:
                case ErrorStatus.NullObjectPointer:
                    return "A reference to a required object was empty. A previous step probably did not " +
                           "produce the object this step depends on.";
                case ErrorStatus.InvalidInput:
                    return "One or more input values were invalid for this operation. Check the numeric " +
                           "parameters (widths, slopes, intervals) in appsettings.json.";
                case ErrorStatus.OutOfRange:
                    return "A value (such as a station or an offset) was outside the allowed range for " +
                           "the alignment or surface.";
                case ErrorStatus.NotApplicable:
                    return "The operation does not apply in the current context (for example, sampling a " +
                           "surface outside its boundary).";
                case ErrorStatus.NotOpenForWrite:
                    return "An object was opened read-only but the operation needs to modify it. This is " +
                           "an internal transaction issue; retry the run.";
                case ErrorStatus.FileAccessErr:
                case ErrorStatus.FileNotFound:
                    return "A file could not be accessed. Confirm the DWG/template path exists and is not " +
                           "open in another program, and that you have permission to it.";
                case ErrorStatus.FileSharingViolation:
                    return "The file is locked by another process. Close the drawing/PDF in any other " +
                           "program and try again.";
                case ErrorStatus.InsufficientMemory:
                    return "Civil 3D ran out of memory during the operation. Try a shorter road segment " +
                           "or a coarser sampling interval.";
                case ErrorStatus.CannotBeErasedByCaller:
                case ErrorStatus.CannotBeResurrected:
                    return "An object could not be modified because it is referenced by other objects " +
                           "(e.g. deleting a surface still used by a profile).";
                case ErrorStatus.NoInputFiler:
                    return "Internal serialization error while copying objects between drawings. The " +
                           "source object may be corrupt.";
                case ErrorStatus.OK:
                    return "The operation reported success but an exception was still raised; see details.";
                default:
                    return "Civil 3D reported an error while performing the operation.";
            }
        }
    }
}
