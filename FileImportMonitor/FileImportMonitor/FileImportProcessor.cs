using System;
using System.IO;
using System.Threading;

namespace FileImportMonitor
{
    /// <summary>
    /// Validates a single file against the configured masks and moves it
    /// to the appropriate destination directory.
    /// </summary>
    internal sealed class FileImportProcessor
    {
        private readonly AppSettings _settings;
        private readonly MaskRepository _maskRepository;
        private readonly Logger _logger;

        public FileImportProcessor(AppSettings settings, MaskRepository maskRepository, Logger logger)
        {
            _settings = settings;
            _maskRepository = maskRepository;
            _logger = logger;
        }

        public void Process(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            if (!WaitUntilFileIsReady(filePath))
            {
                _logger.Warn($"Gave up waiting for '{fileName}' to finish being written; skipping it for now.");
                return;
            }

            if (!File.Exists(filePath))
            {
                // Deleted, renamed away, or already handled by another event.
                return;
            }

            var masks = _maskRepository.GetMasks();
            if (masks.Count == 0)
            {
                _logger.Warn($"No valid masks are configured in {_settings.MaskTableName}; '{fileName}' cannot be validated.");
            }

            bool isValid = false;
            string matchedMask = null;
            foreach (string mask in masks)
            {
                if (FileNameMatcher.IsMatch(fileName, mask))
                {
                    isValid = true;
                    matchedMask = mask;
                    break;
                }
            }

            if (isValid)
            {
                MoveFile(filePath, fileName, _settings.ImportDirectory);
                _logger.Info($"'{fileName}' matched mask '{matchedMask}' and was moved to '{_settings.ImportDirectory}'.");
            }
            else
            {
                _logger.Warn($"'{fileName}' did not match any mask in {_settings.MaskTableName}.");

                if (!string.IsNullOrWhiteSpace(_settings.RejectedDirectory))
                {
                    MoveFile(filePath, fileName, _settings.RejectedDirectory);
                    _logger.Info($"'{fileName}' was moved to rejected directory '{_settings.RejectedDirectory}'.");
                }
            }
        }

        /// <summary>
        /// Files can appear before the writer has finished (large copies,
        /// slow network drops, etc). Poll until the file can be opened
        /// exclusively, or until the configured timeout elapses.
        /// </summary>
        private bool WaitUntilFileIsReady(string filePath)
        {
            var timeout = TimeSpan.FromSeconds(_settings.FileStabilizationTimeoutSeconds);
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                try
                {
                    using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(500);
                }
            }

            return false;
        }

        private void MoveFile(string sourcePath, string fileName, string destinationDirectory)
        {
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string destinationPath = Path.Combine(destinationDirectory, fileName);

            if (File.Exists(destinationPath))
            {
                string uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMdd_HHmmssfff}{Path.GetExtension(fileName)}";
                destinationPath = Path.Combine(destinationDirectory, uniqueName);
                _logger.Warn($"A file named '{fileName}' already exists in '{destinationDirectory}'; moving as '{uniqueName}' instead.");
            }

            File.Move(sourcePath, destinationPath);
        }
    }
}
