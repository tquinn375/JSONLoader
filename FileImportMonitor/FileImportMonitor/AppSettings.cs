using System;
using System.Configuration;
using System.Data.Odbc;
using System.IO;

namespace FileImportMonitor
{
    /// <summary>
    /// Strongly-typed view over App.config's &lt;appSettings&gt; / &lt;connectionStrings&gt;.
    /// </summary>
    internal sealed class AppSettings
    {
        /// <summary>
        /// The ODBC connection string from App.config. It is expected NOT
        /// to carry a password — that's supplied separately at runtime and
        /// merged in via <see cref="BuildConnectionString"/>.
        /// </summary>
        public string ConnectionStringTemplate { get; }
        public string WatchDirectory { get; }
        public string ImportDirectory { get; }
        public string RejectedDirectory { get; }
        public string MaskTableName { get; }
        public string MaskColumnName { get; }
        public string MaskActiveColumnName { get; }
        public string MaskActiveValue { get; }
        public int MaskRefreshIntervalSeconds { get; }
        public int FileStabilizationTimeoutSeconds { get; }
        public bool ProcessExistingFilesOnStartup { get; }
        public string LogFilePath { get; }

        private AppSettings(
            string connectionStringTemplate,
            string watchDirectory,
            string importDirectory,
            string rejectedDirectory,
            string maskTableName,
            string maskColumnName,
            string maskActiveColumnName,
            string maskActiveValue,
            int maskRefreshIntervalSeconds,
            int fileStabilizationTimeoutSeconds,
            bool processExistingFilesOnStartup,
            string logFilePath)
        {
            ConnectionStringTemplate = connectionStringTemplate;
            WatchDirectory = watchDirectory;
            ImportDirectory = importDirectory;
            RejectedDirectory = rejectedDirectory;
            MaskTableName = maskTableName;
            MaskColumnName = maskColumnName;
            MaskActiveColumnName = maskActiveColumnName;
            MaskActiveValue = maskActiveValue;
            MaskRefreshIntervalSeconds = maskRefreshIntervalSeconds;
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
            var connectionStringSetting = ConfigurationManager.ConnectionStrings["ImportValidationDb"];
            if (connectionStringSetting == null || string.IsNullOrWhiteSpace(connectionStringSetting.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "Missing connection string 'ImportValidationDb' in App.config.");
            }

            string watchDirectory = RequireSetting("WatchDirectory");
            string importDirectory = RequireSetting("ImportDirectory");
            string rejectedDirectory = ConfigurationManager.AppSettings["RejectedDirectory"] ?? string.Empty;

            string maskTableName = ConfigurationManager.AppSettings["MaskTableName"];
            if (string.IsNullOrWhiteSpace(maskTableName))
            {
                maskTableName = "LOCAL_IMPORTFILEVALIDMASKS";
            }

            string maskColumnName = ConfigurationManager.AppSettings["MaskColumnName"];
            if (string.IsNullOrWhiteSpace(maskColumnName))
            {
                maskColumnName = "FILEMASK";
            }

            string maskActiveColumnName = ConfigurationManager.AppSettings["MaskActiveColumnName"] ?? string.Empty;
            string maskActiveValue = ConfigurationManager.AppSettings["MaskActiveValue"] ?? "Y";

            int maskRefreshIntervalSeconds = ReadInt("MaskRefreshIntervalSeconds", 60);
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
                connectionStringSetting.ConnectionString,
                watchDirectory,
                importDirectory,
                rejectedDirectory,
                maskTableName,
                maskColumnName,
                maskActiveColumnName,
                maskActiveValue,
                maskRefreshIntervalSeconds,
                fileStabilizationTimeoutSeconds,
                processExistingFilesOnStartup,
                logFilePath);
        }

        /// <summary>
        /// Merges the runtime-supplied database password into
        /// <see cref="ConnectionStringTemplate"/>, overwriting any "Pwd"
        /// the template already carries. Using OdbcConnectionStringBuilder
        /// (rather than string concatenation) makes sure a password
        /// containing ';', '=', quotes, etc. is escaped correctly.
        /// </summary>
        public string BuildConnectionString(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Database password must not be empty.", nameof(password));
            }

            var builder = new OdbcConnectionStringBuilder(ConnectionStringTemplate)
            {
                ["Pwd"] = password
            };
            return builder.ConnectionString;
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
