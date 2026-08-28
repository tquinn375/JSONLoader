# FileImportMonitor

A Visual C# (.NET Framework 4.8) console application that watches a
directory for newly-arrived files, validates each file name against masks
stored in an ODBC-accessible `LOCAL_IMPORTFILEVALIDMASKS` table, and moves
files that match into `D:\IMPORT` (configurable).

## How it works

1. On startup, and again whenever a file is created (or renamed into place)
   in the watched directory, the app waits for the file to stop being
   written to (it retries opening it exclusively until that succeeds or a
   timeout is hit), so partially-copied files aren't processed early.
2. It loads the list of valid filename masks from
   `LOCAL_IMPORTFILEVALIDMASKS` over ODBC (cached in memory and refreshed on
   an interval, not queried per file).
3. Each mask is a DOS-style wildcard (`*` and `?`), matched
   case-insensitively against the file name, e.g. `INV*.TXT`, `ORD???.CSV`.
4. If the file name matches any mask, the file is moved into
   `ImportDirectory` (`D:\IMPORT` by default). If a same-named file already
   exists there, a timestamp is appended so nothing is overwritten.
5. If the file name matches no mask, it's left in place (or moved to
   `RejectedDirectory`, if one is configured) and logged as a warning.

All activity is written to the console and to a rolling log file
(`Logs\FileImportMonitor.log` by default).

## Project layout

```
FileImportMonitor.sln
FileImportMonitor/
  FileImportMonitor.csproj
  App.config              Configuration: connection string, directories, table/column names
  Program.cs               Entry point / startup / shutdown
  AppSettings.cs            Reads and validates App.config
  Logger.cs                 Console + file logging
  DirectoryMonitor.cs       FileSystemWatcher wrapper with debouncing
  FileImportProcessor.cs    Waits for file to stabilize, validates, moves
  MaskRepository.cs         Loads/caches masks from ODBC
  FileNameMatcher.cs        Wildcard-to-regex matching
```

## Setup

1. **Open `FileImportMonitor.sln` in Visual Studio** (2019+ recommended;
   the project targets .NET Framework 4.8).

2. **Create an ODBC data source** (Windows ODBC Data Source Administrator,
   `odbcad32.exe`) for the database that hosts
   `LOCAL_IMPORTFILEVALIDMASKS`, or use a DSN-less connection string with
   the appropriate ODBC driver.

3. **Edit `FileImportMonitor/App.config`:**
   - `connectionStrings/ImportValidationDb` — your ODBC DSN/credentials.
   - `WatchDirectory` — the folder to monitor for incoming files.
   - `ImportDirectory` — where validated files are moved
     (`D:\IMPORT` by default).
   - `RejectedDirectory` — optional; where non-matching files are moved.
     Leave blank to leave them in `WatchDirectory` instead.
   - `MaskTableName` / `MaskColumnName` — defaults to
     `LOCAL_IMPORTFILEVALIDMASKS` / `FILEMASK`. Change `MaskColumnName` to
     match your actual column name if it differs.
   - `MaskActiveColumnName` / `MaskActiveValue` — optional; if your table
     has an active/enabled flag column, set the column name here (e.g.
     `ACTIVE`) so only rows where it equals `MaskActiveValue` (default
     `Y`) are used. Leave `MaskActiveColumnName` blank to use every row.
   - `MaskRefreshIntervalSeconds` — how often the mask list is re-read
     from the database (default 60s).
   - `FileStabilizationTimeoutSeconds` — how long to wait for a file to
     finish being written before giving up on it (default 30s).
   - `ProcessExistingFilesOnStartup` — set to `false` if you don't want
     files already sitting in `WatchDirectory` processed on startup.

4. **Expected table shape** — the app runs:
   ```sql
   SELECT <MaskColumnName> FROM <MaskTableName>
   -- plus: WHERE <MaskActiveColumnName> = '<MaskActiveValue>', if configured
   ```
   Each row's value is treated as one wildcard mask, e.g.:

   | FILEMASK      |
   |---------------|
   | INV*.TXT      |
   | ORD???.CSV    |
   | *.JSON        |

   Adjust `MaskTableName`/`MaskColumnName`/`MaskActiveColumnName` in
   `App.config` if your schema differs — no code changes needed.

5. **Build and run.** The console window stays open, watching the
   directory; press Ctrl+C to stop it. For unattended use, run it under
   Task Scheduler (on logon, with restart-on-failure) or wrap it as a
   Windows Service.

## Notes

- The connection string in `App.config` is a plain-text credential —
  restrict file permissions on the deployed `App.config`/`.exe.config`
  accordingly, the same as any other service credential.
- Table/column names from `App.config` are trusted, operator-supplied
  configuration (not end-user input), so they're interpolated directly
  into the generated SQL; the `MaskActiveValue` comparison value is
  escaped before use.
