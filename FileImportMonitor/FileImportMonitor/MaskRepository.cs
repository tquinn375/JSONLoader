using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;

namespace FileImportMonitor
{
    /// <summary>
    /// Loads valid filename masks from the LOCAL_IMPORTFILEVALIDMASKS table
    /// over ODBC, caching them in memory and refreshing on a timer so every
    /// file event doesn't require a round-trip to the database.
    /// </summary>
    internal sealed class MaskRepository
    {
        private readonly AppSettings _settings;
        private readonly string _connectionString;
        private readonly Logger _logger;
        private readonly object _cacheLock = new object();

        private List<string> _cachedMasks = new List<string>();
        private DateTime _lastRefreshUtc = DateTime.MinValue;

        /// <summary>
        /// <paramref name="connectionString"/> is the fully-resolved ODBC
        /// connection string (i.e. <see cref="AppSettings.BuildConnectionString"/>
        /// already applied) — the password is merged in once at startup
        /// rather than re-read on every refresh.
        /// </summary>
        public MaskRepository(AppSettings settings, string connectionString, Logger logger)
        {
            _settings = settings;
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Returns the current mask list, refreshing from the database if
        /// the configured refresh interval has elapsed.
        /// </summary>
        public IReadOnlyList<string> GetMasks()
        {
            bool needsRefresh;
            lock (_cacheLock)
            {
                needsRefresh = DateTime.UtcNow - _lastRefreshUtc > TimeSpan.FromSeconds(_settings.MaskRefreshIntervalSeconds);
            }

            if (needsRefresh)
            {
                RefreshFromDatabase();
            }

            lock (_cacheLock)
            {
                return _cachedMasks.ToList();
            }
        }

        private void RefreshFromDatabase()
        {
            try
            {
                var masks = new List<string>();

                using (var connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = BuildSelectStatement();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    string mask = reader.GetValue(0).ToString().Trim();
                                    if (mask.Length > 0)
                                    {
                                        masks.Add(mask);
                                    }
                                }
                            }
                        }
                    }
                }

                lock (_cacheLock)
                {
                    _cachedMasks = masks;
                    _lastRefreshUtc = DateTime.UtcNow;
                }

                _logger.Info($"Loaded {masks.Count} mask(s) from {_settings.MaskTableName}.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to refresh masks from {_settings.MaskTableName}; keeping previously cached masks.", ex);

                // Avoid hammering a database that's down: push the next
                // retry out by the normal refresh interval.
                lock (_cacheLock)
                {
                    _lastRefreshUtc = DateTime.UtcNow;
                }
            }
        }

        private string BuildSelectStatement()
        {
            string sql = $"SELECT {_settings.MaskColumnName} FROM {_settings.MaskTableName}";

            if (!string.IsNullOrWhiteSpace(_settings.MaskActiveColumnName))
            {
                string escapedValue = _settings.MaskActiveValue.Replace("'", "''");
                sql += $" WHERE {_settings.MaskActiveColumnName} = '{escapedValue}'";
            }

            return sql;
        }
    }
}
