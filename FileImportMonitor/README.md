# FileImportMonitor

A Visual C# (.NET Framework 4.8) console application that watches a
directory for newly-arrived files, validates each file name against a list
of authorized masks configured in `App.config`, and moves files that match
into `D:\IMPORT` (configurable).

## How it works

1. On startup, and again whenever a file is created (or renamed into place)
   in the watched directory, the app waits for the file to stop being
   written to (it retries opening it exclusively until that succeeds or a
   timeout is hit), so partially-copied files aren't processed early.
2. Each configured mask is a DOS-style wildcard (`*` and `?`), matched
   case-insensitively against the file name, e.g. `INV*.TXT`, `ORD???.CSV`.
3. If the file name matches any mask, the file is moved into
   `ImportDirectory` (`D:\IMPORT` by default). If a same-named file already
   exists there, a timestamp is appended so nothing is overwritten.
4. If the file name matches no mask, it's left in place (or moved to
   `RejectedDirectory`, if one is configured) and logged as a warning.
5. The app runs for `RunDurationMinutes` (default 120 = 2 hours) and then
   exits cleanly on its own — see "Running as a scheduled task" below.

All activity is written to the console and to a rolling log file
(`Logs\FileImportMonitor.log` by default).

## Project layout

```
FileImportMonitor.sln
FileImportMonitor/
  FileImportMonitor.csproj
  App.config              Configuration: directories, valid file masks
  Program.cs               Entry point / startup / shutdown
  AppSettings.cs            Reads and validates App.config
  Logger.cs                 Console + file logging
  DirectoryMonitor.cs       FileSystemWatcher wrapper with debouncing
  FileImportProcessor.cs    Waits for file to stabilize, validates, moves
  FileNameMatcher.cs        Wildcard-to-regex matching
```

## Setup

1. **Open `FileImportMonitor.sln` in Visual Studio** (2019+ recommended;
   the project targets .NET Framework 4.8).

2. **Edit `FileImportMonitor/App.config`:**
   - `WatchDirectory` — the folder to monitor for incoming files.
   - `ImportDirectory` — where validated files are moved
     (`D:\IMPORT` by default).
   - `RejectedDirectory` — optional; where non-matching files are moved.
     Leave blank to leave them in `WatchDirectory` instead.
   - `ValidFileMasks` — semicolon-delimited list of authorized filename
     masks, e.g. `INV*.TXT;ORD???.CSV;*.JSON`. A file is only moved into
     `ImportDirectory` if its name matches one of these.
   - `FileStabilizationTimeoutSeconds` — how long to wait for a file to
     finish being written before giving up on it (default 30s).
   - `ProcessExistingFilesOnStartup` — set to `false` if you don't want
     files already sitting in `WatchDirectory` processed on startup.
   - `RunDurationMinutes` — how many minutes the app watches before
     exiting on its own (default `120`). Ctrl+C still stops it sooner for
     interactive use.

3. **Build and run.** The console window stays open, watching the
   directory, until either `RunDurationMinutes` elapses or you press
   Ctrl+C.

## Running as a scheduled task

The app is designed to be launched repeatedly by Windows Task Scheduler
rather than run once and left open:

- Each run watches for `RunDurationMinutes` (2 hours by default) and then
  exits with code `0`.
- On startup it takes a system-wide named mutex
  (`Global\FileImportMonitor_SingleInstance`). If another copy already
  holds it — e.g. the previous scheduled run is still inside its 2-hour
  window when the next one fires — the new instance logs a warning and
  exits immediately with code `2`, without touching the watch directory.
  Only one instance is ever doing work at a time.

Suggested Task Scheduler setup: trigger every 2 hours (matching
`RunDurationMinutes`, or shorter — the mutex check makes an overlapping
trigger a safe no-op rather than a second monitor), "Run whether user is
logged on or not," and *do not* check "If the task is already running,
then the following rule applies" as a substitute for this — the app's own
mutex check is what actually guarantees a single instance; Task
Scheduler's own instance-handling setting can still be left at its default
since the app self-terminates duplicates either way.

Exit codes: `0` normal completion (duration elapsed or Ctrl+C), `1`
configuration or unhandled error, `2` another instance was already
running.

## Notes

- Masks are read from `App.config` once at startup. To change the
  authorized mask list, edit `ValidFileMasks` and restart the app.
