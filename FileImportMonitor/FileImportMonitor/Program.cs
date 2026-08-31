using System;
using System.IO;
using System.Threading;

namespace FileImportMonitor
{
    internal static class Program
    {
        private static int Main(string[] args)
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

            string password;
            try
            {
                password = ResolveDbPassword(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 1;
            }

            var logger = new Logger(settings.LogFilePath);

            string connectionString;
            try
            {
                connectionString = settings.BuildConnectionString(password);
            }
            catch (Exception ex)
            {
                logger.Error("Failed to build the ODBC connection string from App.config + the supplied password.", ex);
                return 1;
            }

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

                var maskRepository = new MaskRepository(settings, connectionString, logger);
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

        /// <summary>
        /// Gets the import-validation database password from the command
        /// line (<c>--password VALUE</c>, <c>--password=VALUE</c>, or
        /// <c>-p VALUE</c>). If it wasn't passed and a console is attached,
        /// prompts for it with masked input instead of requiring it to
        /// appear in a command line / scheduled task definition.
        /// </summary>
        private static string ResolveDbPassword(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("--password=", StringComparison.OrdinalIgnoreCase))
                {
                    return RequireNonEmpty(arg.Substring("--password=".Length));
                }

                bool isNamedFlag = string.Equals(arg, "--password", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "-p", StringComparison.OrdinalIgnoreCase);
                if (isNamedFlag)
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException($"'{arg}' was given without a value.");
                    }
                    return RequireNonEmpty(args[i + 1]);
                }
            }

            if (Console.IsInputRedirected)
            {
                throw new ArgumentException(
                    "No database password supplied and no console is attached to prompt for one. " +
                    "Pass it explicitly, e.g. --password \"the_password\".");
            }

            return ReadPasswordFromConsole();
        }

        private static string RequireNonEmpty(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("The database password argument must not be empty.");
            }
            return value;
        }

        private static string ReadPasswordFromConsole()
        {
            Console.Write("Import validation DB password: ");
            var password = new System.Text.StringBuilder();

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Length--;
                        Console.Write("\b \b");
                    }
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write('*');
                }
            }

            return RequireNonEmpty(password.ToString());
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: FileImportMonitor.exe --password <db-password>");
            Console.Error.WriteLine("       FileImportMonitor.exe -p <db-password>");
            Console.Error.WriteLine("Omit the argument to be prompted for the password interactively.");
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
