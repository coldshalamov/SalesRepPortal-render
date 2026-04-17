using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadManagementPortal.Data
{
    public static class SqliteCommissionSchemaCompatibility
    {
        private static readonly string[] RequiredTables =
        {
            "BusinessAccounts",
            "CommissionAdjustments",
            "CommissionAgreements",
            "CommissionAgreementRecipients",
            "CommissionDeals",
            "CommissionLinks",
            "ImportBatches",
            "ImportProfiles",
            "ImportRows",
            "PayoutBatches",
            "PayoutEntries",
            "SaleEvents",
            "SaleRecords",
            "CommissionLedgerEntries",
            "CommissionLedgers"
        };

        public static async Task EnsureCommissionSchemaAsync(
            ApplicationDbContext db,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            if (!db.Database.IsSqlite())
            {
                return;
            }

            var existingTables = await ReadExistingTablesAsync(db, cancellationToken);
            var missingTables = RequiredTables
                .Where(table => !existingTables.Contains(table))
                .ToArray();

            if (missingTables.Length == 0)
            {
                return;
            }

            if (existingTables.Count > 0)
            {
                var connectionString = db.Database.GetDbConnection().ConnectionString;
                if (!IsEphemeralSqliteDataSource(connectionString))
                {
                    var missingTablesList = string.Join(", ", missingTables);
                    var dataSource = DescribeSqliteDataSource(connectionString);
                    var message = $"SQLite database '{dataSource}' is missing current commission schema tables: {missingTablesList}. Refusing to recreate a non-ephemeral SQLite database because that could delete real commission/accounting history. Run a proper migration or move this environment to the production database provider.";

                    logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                logger.LogWarning(
                    "SQLite database is missing current commission schema tables: {MissingTables}. Recreating the SQLite database so the latest schema is applied.",
                    string.Join(", ", missingTables));

                await db.Database.EnsureDeletedAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("SQLite database does not have any application tables yet. Creating the latest schema.");
            }

            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        private static async Task<HashSet<string>> ReadExistingTablesAsync(
            ApplicationDbContext db,
            CancellationToken cancellationToken)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        var name = reader.GetString(0);
                        if (!name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
                        {
                            tables.Add(name);
                        }
                    }
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return tables;
        }

        private static bool IsEphemeralSqliteDataSource(string connectionString)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource?.Trim();

            if (string.IsNullOrWhiteSpace(dataSource))
            {
                return false;
            }

            if (string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (dataSource.Contains("://", StringComparison.Ordinal))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(dataSource);
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string DescribeSqliteDataSource(string connectionString)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.DataSource) ? "(unknown datasource)" : builder.DataSource;
        }
    }
}
