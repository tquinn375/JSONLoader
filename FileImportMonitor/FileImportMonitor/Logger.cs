using System;
using System.IO;
using System.Text;

namespace FileImportMonitor
{
    internal enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// Minimal thread-safe logger that writes timestamped lines to the
    /// console and to a log file, creating the log directory if needed.
    /// </summary>
    internal sealed class Logger
    {
        private readonly string _logFilePath;
        private readonly object _writeLock = new object();

        /// <summary>The actual, timestamped log file this instance writes to.</summary>
        public string LogFilePath => _logFilePath;

        /// <summary>
        /// <paramref name="baseLogFilePath"/> is the configured path (e.g.
        /// "Logs\FileImportMonitor.log"). A run-start timestamp is inserted
        /// before the extension so each run gets its own file and earlier
        /// runs' logs are never overwritten.
        /// </summary>
        public Logger(string baseLogFilePath)
        {
            _logFilePath = BuildTimestampedPath(baseLogFilePath);
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string BuildTimestampedPath(string baseLogFilePath)
        {
            string directory = Path.GetDirectoryName(baseLogFilePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(baseLogFilePath);
            string extension = Path.GetExtension(baseLogFilePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string timestampedFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";

            return string.IsNullOrEmpty(directory)
                ? timestampedFileName
                : Path.Combine(directory, timestampedFileName);
        }

        public void Info(string message) => Write(LogLevel.Info, message);
        public void Warn(string message) => Write(LogLevel.Warn, message);
        public void Error(string message) => Write(LogLevel.Error, message);

        public void Error(string message, Exception ex)
        {
            Write(LogLevel.Error, $"{message} :: {ex}");
        }

        private void Write(LogLevel level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level.ToString().ToUpperInvariant()}] {message}";

            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Warn => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => originalColor
            };
            Console.WriteLine(line);
            Console.ForegroundColor = originalColor;

            lock (_writeLock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException)
                {
                    // Best-effort file logging; console output above already happened.
                }
            }
        }
    }
}
