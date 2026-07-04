using System;
using System.IO;
using System.Text;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.Logging
{
    /// <summary>
    /// Thread-safe logger that appends entries to a single per-run log file. Entries below the
    /// configured minimum level are ignored. Writes are guarded by a lock and every I/O operation is
    /// wrapped so that a logging failure (e.g. locked file) can never crash the workflow.
    /// </summary>
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly object _gate = new object();
        private readonly LogLevel _minimumLevel;
        private readonly string _filePath;
        private StreamWriter _writer;
        private bool _disposed;

        /// <summary>Absolute path of the log file being written.</summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Creates a file logger. The target directory is created if needed. If the file cannot be
        /// opened the logger degrades to a no-op rather than throwing.
        /// </summary>
        /// <param name="filePath">Full path of the log file to append to.</param>
        /// <param name="minimumLevel">Minimum level to persist.</param>
        public FileLogger(string filePath, LogLevel minimumLevel)
        {
            _filePath = filePath;
            _minimumLevel = minimumLevel;

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8)
                {
                    AutoFlush = true
                };

                _writer.WriteLine();
                _writer.WriteLine("===== Log session started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====");
            }
            catch
            {
                // Could not open the file (permissions, path). Degrade silently to no-op.
                _writer = null;
            }
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, string category = "", Exception exception = null)
        {
            if (level < _minimumLevel) return;
            if (_writer == null) return;

            var entry = new LogEntry(DateTime.UtcNow, level, message, category, exception);

            lock (_gate)
            {
                if (_disposed || _writer == null) return;
                try
                {
                    _writer.WriteLine(entry.ToLogLine());
                }
                catch
                {
                    // Never allow a logging failure to propagate.
                }
            }
        }

        /// <summary>Flushes and closes the underlying file.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    if (_writer != null)
                    {
                        _writer.WriteLine("===== Log session ended " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====");
                        _writer.Flush();
                        _writer.Dispose();
                    }
                }
                catch
                {
                    // Ignore dispose failures.
                }
                finally
                {
                    _writer = null;
                }
            }
        }
    }
}
