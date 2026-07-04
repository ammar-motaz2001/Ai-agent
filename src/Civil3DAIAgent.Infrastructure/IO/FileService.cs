using System;
using System.IO;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Infrastructure.IO
{
    /// <summary>
    /// Default <see cref="IFileService"/> over <see cref="System.IO"/>. Every method is defensive:
    /// expected conditions return results, and even unexpected I/O exceptions are caught and turned
    /// into failed results rather than propagating (supporting the "never crash" policy).
    /// </summary>
    public sealed class FileService : IFileService
    {
        private readonly ILogger _logger;

        /// <summary>Creates the service.</summary>
        public FileService(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && File.Exists(path); }
            catch { return false; }
        }

        /// <inheritdoc />
        public bool DirectoryExists(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path); }
            catch { return false; }
        }

        /// <inheritdoc />
        public OperationResult EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return OperationResult.Fail("No folder path was provided.");

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.Debug("Created directory: " + path, "File");
                }
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    "Could not create or access the folder '" + path + "'. " +
                    "Check the path is valid and that you have write permission. Details: " + ex.Message, ex);
            }
        }

        /// <inheritdoc />
        public string GetUniquePath(string folder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folder)) folder = ".";
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "output";

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var candidate = Path.Combine(folder, name + ext);

            int counter = 1;
            try
            {
                while (File.Exists(candidate))
                {
                    candidate = Path.Combine(folder, string.Format("{0} ({1}){2}", name, counter, ext));
                    counter++;
                }
            }
            catch
            {
                // If probing fails, return the original candidate; the caller's write will surface issues.
            }

            return candidate;
        }

        /// <inheritdoc />
        public OperationResult<int> PurgeOldLogs(string logFolder, int retainDays)
        {
            if (string.IsNullOrWhiteSpace(logFolder) || !Directory.Exists(logFolder) || retainDays <= 0)
                return OperationResult<int>.Ok(0);

            int purged = 0;
            try
            {
                var cutoff = DateTime.Now.AddDays(-retainDays);
                foreach (var file in Directory.GetFiles(logFolder, "*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                        {
                            File.Delete(file);
                            purged++;
                        }
                    }
                    catch
                    {
                        // Skip files that are locked or not deletable.
                    }
                }

                if (purged > 0)
                    _logger.Debug($"Purged {purged} log file(s) older than {retainDays} days.", "File");

                return OperationResult<int>.Ok(purged);
            }
            catch (Exception ex)
            {
                return OperationResult<int>.Fail("Failed while purging old logs: " + ex.Message, ex);
            }
        }
    }
}
