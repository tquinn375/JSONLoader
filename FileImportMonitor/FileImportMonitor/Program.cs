using System;
using System.IO;
using System.Threading;

namespace FileImportMonitor
{
    internal static class Program
    {
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
                return 1;
            }

            var logger = new Logger(settings.LogFilePath);

            try
            {
                if (!Directory.Exists(settings.WatchDirectory))
                {
                    logger.Error($"Watch directory '{settings.WatchDirectory}' does not exist. Create it or update App.config, then restart.");
                    return 1;
                }

                if (!Directory.Exists(settings.ImportDirectory))
                {
                    logger.Info($"Import directory '{settings.ImportDirectory}' does not exist; creating it.");
                    Directory.CreateDirectory(settings.ImportDirectory);
                }

                var maskRepository = new MaskRepository(settings, logger);
                var processor = new FileImportProcessor(settings, maskRepository, logger);

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

                    Console.WriteLine("FileImportMonitor is running. Press Ctrl+C to exit.");
                    exitSignal.Wait();

                    monitor.Stop();
                }

                logger.Info("FileImportMonitor stopped.");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error("Unhandled exception; FileImportMonitor is shutting down.", ex);
                return 1;
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
