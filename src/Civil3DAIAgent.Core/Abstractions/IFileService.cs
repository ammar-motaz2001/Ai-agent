using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// File-system operations abstracted for testability and centralized error handling. Implemented
    /// in the Infrastructure layer. All members are defensive: they return results rather than
    /// throwing for expected conditions (missing file, unwritable folder).
    /// </summary>
    public interface IFileService
    {
        /// <summary>Returns true when the file exists and is readable.</summary>
        bool FileExists(string path);

        /// <summary>Returns true when the directory exists.</summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Ensures the directory exists, creating it (and parents) if needed. Returns a failed result
        /// with a plain-language message if creation is not possible.
        /// </summary>
        OperationResult EnsureDirectory(string path);

        /// <summary>
        /// Builds a unique output path inside <paramref name="folder"/> for the given base file name,
        /// appending " (n)" before the extension if a file already exists so nothing is overwritten.
        /// </summary>
        string GetUniquePath(string folder, string fileName);

        /// <summary>
        /// Deletes log files older than <paramref name="retainDays"/> in <paramref name="logFolder"/>.
        /// Never throws; returns the number of files purged.
        /// </summary>
        OperationResult<int> PurgeOldLogs(string logFolder, int retainDays);
    }
}
