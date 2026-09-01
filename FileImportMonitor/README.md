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

3. **Build and run.** The console window stays open, watching the
   directory; press Ctrl+C to stop it. For unattended use, run it under
   Task Scheduler (on logon, with restart-on-failure) or wrap it as a
   Windows Service.

## Notes

- Masks are read from `App.config` once at startup. To change the
  authorized mask list, edit `ValidFileMasks` and restart the app.
