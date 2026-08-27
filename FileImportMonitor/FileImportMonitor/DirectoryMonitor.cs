using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace FileImportMonitor
{
    /// <summary>
    /// Wraps a FileSystemWatcher for a single directory. New files (via
    /// Created or Renamed-into-place events) are debounced per-path so a
    /// burst of I/O activity for one file only triggers one processing
    /// pass, then handed off to the supplied processor on a background
    /// thread so slow moves/ODBC calls never block the watcher.
    /// </summary>
    internal sealed class DirectoryMonitor : IDisposable
    {
        private const int DebounceMilliseconds = 1000;

        private readonly string _watchDirectory;
        private readonly FileImportProcessor _processor;
        private readonly Logger _logger;
        private readonly FileSystemWatcher _watcher;
        private readonly ConcurrentDictionary<string, Timer> _pendingTimers = new ConcurrentDictionary<string, Timer>();

        public DirectoryMonitor(string watchDirectory, FileImportProcessor processor, Logger logger)
        {
            _watchDirectory = watchDirectory;
            _processor = processor;
            _logger = logger;

            _watcher = new FileSystemWatcher(_watchDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false
            };
            _watcher.Created += (sender, e) => ScheduleProcessing(e.FullPath);
            _watcher.Renamed += (sender, e) => ScheduleProcessing(e.FullPath);
            _watcher.Error += OnWatcherError;
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
            _logger.Info($"Watching '{_watchDirectory}' for new files.");
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void ScheduleProcessing(string fullPath)
        {
            // Reset any existing debounce timer for this path so repeated
            // write events for the same file only fire processing once,
            // after activity on it has quieted down.
            _pendingTimers.AddOrUpdate(
                fullPath,
                key => new Timer(OnDebounceElapsed, key, DebounceMilliseconds, Timeout.Infinite),
                (key, existingTimer) =>
                {
                    existingTimer.Change(DebounceMilliseconds, Timeout.Infinite);
                    return existingTimer;
                });
        }

        private void OnDebounceElapsed(object state)
        {
            string fullPath = (string)state;

            if (_pendingTimers.TryRemove(fullPath, out Timer timer))
            {
                timer.Dispose();
            }

            if (!File.Exists(fullPath))
            {
                return;
            }

            try
            {
                _processor.Process(fullPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unhandled error processing '{fullPath}'.", ex);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.Error("FileSystemWatcher encountered an error; attempting to restart it.", e.GetException());

            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to restart FileSystemWatcher.", ex);
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();

            foreach (var timer in _pendingTimers.Values)
            {
                timer.Dispose();
            }
            _pendingTimers.Clear();
        }
    }
}
