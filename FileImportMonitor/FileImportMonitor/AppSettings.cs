using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace FileImportMonitor
{
    /// <summary>
    /// Strongly-typed view over App.config's &lt;appSettings&gt;.
    /// </summary>
    internal sealed class AppSettings
    {
        public string WatchDirectory { get; }
        public string ImportDirectory { get; }
        public string RejectedDirectory { get; }
        public IReadOnlyList<string> ValidFileMasks { get; }
        public int FileStabilizationTimeoutSeconds { get; }
        public bool ProcessExistingFilesOnStartup { get; }
        public string LogFilePath { get; }

        private AppSettings(
            string watchDirectory,
            string importDirectory,
            string rejectedDirectory,
            IReadOnlyList<string> validFileMasks,
            int fileStabilizationTimeoutSeconds,
            bool processExistingFilesOnStartup,
            string logFilePath)
        {
            WatchDirectory = watchDirectory;
            ImportDirectory = importDirectory;
            RejectedDirectory = rejectedDirectory;
            ValidFileMasks = validFileMasks;
            FileStabilizationTimeoutSeconds = fileStabilizationTimeoutSeconds;
            ProcessExistingFilesOnStartup = processExistingFilesOnStartup;
            LogFilePath = logFilePath;
        }

        /// <summary>
        /// Reads and validates configuration. Throws with a clear message
        /// if something required is missing or malformed.
        /// </summary>
        public static AppSettings Load()
        {
            string watchDirectory = RequireSetting("WatchDirectory");
            string importDirectory = RequireSetting("ImportDirectory");
            string rejectedDirectory = ConfigurationManager.AppSettings["RejectedDirectory"] ?? string.Empty;

            IReadOnlyList<string> validFileMasks = ReadMasks();

            int fileStabilizationTimeoutSeconds = ReadInt("FileStabilizationTimeoutSeconds", 30);
            bool processExistingFilesOnStartup = ReadBool("ProcessExistingFilesOnStartup", true);

            string logFilePath = ConfigurationManager.AppSettings["LogFilePath"];
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                logFilePath = Path.Combine("Logs", "FileImportMonitor.log");
            }
            if (!Path.IsPathRooted(logFilePath))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                logFilePath = Path.Combine(baseDir, logFilePath);
            }

            return new AppSettings(
                watchDirectory,
                importDirectory,
                rejectedDirectory,
                validFileMasks,
                fileStabilizationTimeoutSeconds,
                processExistingFilesOnStartup,
                logFilePath);
        }

        /// <summary>
        /// Reads the semicolon-delimited list of authorized filename masks
        /// from the "ValidFileMasks" appSetting, e.g.
        /// "INV*.TXT;ORD???.CSV;*.JSON".
        /// </summary>
        private static IReadOnlyList<string> ReadMasks()
        {
            string raw = ConfigurationManager.AppSettings["ValidFileMasks"] ?? string.Empty;

            return raw.Split(';')
                .Select(mask => mask.Trim())
                .Where(mask => mask.Length > 0)
                .ToList();
        }

        private static string RequireSetting(string key)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException($"Missing required appSetting '{key}' in App.config.");
            }
            return value;
        }

        private static int ReadInt(string key, int defaultValue)
        {
            string raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }
            if (!int.TryParse(raw, out int value) || value <= 0)
            {
                throw new ConfigurationErrorsException($"appSetting '{key}' must be a positive integer.");
            }
            return value;
        }

        private static bool ReadBool(string key, bool defaultValue)
        {
            string raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }
            if (!bool.TryParse(raw, out bool value))
            {
                throw new ConfigurationErrorsException($"appSetting '{key}' must be 'true' or 'false'.");
            }
            return value;
        }
    }
}
