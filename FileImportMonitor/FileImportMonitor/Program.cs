using System;
using System.IO;
using System.Threading;

namespace FileImportMonitor
{
    internal static class Program
    {
        /// <summary>
        /// "Global\" makes this visible across Terminal Server / RDP
        /// sessions (e.g. a Task Scheduler run in Session 0 vs. someone
        /// running the exe by hand in their own session) so two copies
        /// never run at once regardless of how each was launched.
        /// </summary>
        private const string SingleInstanceMutexName = @"Global\FileImportMonitor_SingleInstance";

        private const int ExitOk = 0;
        private const int ExitError = 1;
        private const int ExitAlreadyRunning = 2;

        private static int Main()
        {
            AppSettings settings;
            try
            {
                settings = AppSettings.Load();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Configuration error: {ex.Message}");
                return ExitError;
            }

            var logger = new Logger(settings.LogFilePath);
            logger.Info($"Logging this run to '{logger.LogFilePath}'.");

            using (Mutex singleInstanceMutex = CreateSingleInstanceMutex(out bool createdNew))
            {
                if (!createdNew)
                {
                    logger.Warn("Another instance of FileImportMonitor is already running; exiting immediately.");
                    return ExitAlreadyRunning;
                }

                try
                {
                    return Run(settings, logger);
                }
                finally
                {
                    singleInstanceMutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Creates (and, per Mutex semantics, atomically takes ownership
        /// of) the system-wide single-instance mutex. Falls back to a
        /// session-local mutex if the account lacks rights to create
        /// "Global\" kernel objects, which still prevents duplicate runs
        /// within the same Task Scheduler / user session.
        /// </summary>
        private static Mutex CreateSingleInstanceMutex(out bool createdNew)
        {
            try
            {
                return new Mutex(initiallyOwned: true, SingleInstanceMutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                string localName = SingleInstanceMutexName.Substring("Global\\".Length);
                return new Mutex(initiallyOwned: true, localName, out createdNew);
            }
        }

        private static int Run(AppSettings settings, Logger logger)
        {
            try
            {
                if (!Directory.Exists(settings.WatchDirectory))
                {
                    logger.Error($"Watch directory '{settings.WatchDirectory}' does not exist. Create it or update App.config, then restart.");
                    return ExitError;
                }

                if (!Directory.Exists(settings.ImportDirectory))
                {
                    logger.Info($"Import directory '{settings.ImportDirectory}' does not exist; creating it.");
                    Directory.CreateDirectory(settings.ImportDirectory);
                }

                if (settings.ValidFileMasks.Count == 0)
                {
                    logger.Warn("No masks are configured in App.config's ValidFileMasks setting; no files will validate.");
                }
                else
                {
                    logger.Info($"Loaded {settings.ValidFileMasks.Count} mask(s) from App.config: {string.Join(", ", settings.ValidFileMasks)}");
                }

                var processor = new FileImportProcessor(settings, logger);

                using (var monitor = new DirectoryMonitor(settings.WatchDirectory, processor, logger))
                {
                    if (settings.ProcessExistingFilesOnStartup)
                    {
                        ProcessExistingFiles(settings.WatchDirectory, processor, logger);
                    }

                    monitor.Start();

                    var exitSignal = new ManualResetEventSlim(false);
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        logger.Info("Shutdown requested (Ctrl+C).");
                        exitSignal.Set();
                    };

                    var runDuration = TimeSpan.FromMinutes(settings.RunDurationMinutes);
                    Console.WriteLine($"FileImportMonitor is running; it will stop automatically after {settings.RunDurationMinutes} minute(s), or press Ctrl+C to exit sooner.");
                    bool signaled = exitSignal.Wait(runDuration);
                    if (!signaled)
                    {
                        logger.Info($"Configured run duration ({settings.RunDurationMinutes} minute(s)) elapsed; shutting down.");
                    }

                    monitor.Stop();
                }

                logger.Info("FileImportMonitor stopped.");
                return ExitOk;
            }
            catch (Exception ex)
            {
                logger.Error("Unhandled exception; FileImportMonitor is shutting down.", ex);
                return ExitError;
            }
        }

        private static void ProcessExistingFiles(string watchDirectory, FileImportProcessor processor, Logger logger)
        {
            string[] existingFiles;
            try
            {
                existingFiles = Directory.GetFiles(watchDirectory);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to enumerate existing files in '{watchDirectory}'.", ex);
                return;
            }

            if (existingFiles.Length == 0)
            {
                return;
            }

            logger.Info($"Processing {existingFiles.Length} existing file(s) in '{watchDirectory}'.");
            foreach (string filePath in existingFiles)
            {
                try
                {
                    processor.Process(filePath);
                }
                catch (Exception ex)
                {
                    logger.Error($"Unhandled error processing existing file '{filePath}'.", ex);
                }
            }
        }
    }
}
