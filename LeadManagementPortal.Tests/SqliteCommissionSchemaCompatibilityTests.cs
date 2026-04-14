using LeadManagementPortal.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadManagementPortal.Tests
{
    public class SqliteCommissionSchemaCompatibilityTests
    {
        [Fact]
        public async Task EnsureCommissionSchemaAsync_CreatesMissingCommissionTablesAndIndexes()
        {
            var dbPath = await CreateLegacySqliteDatabaseAsync();

            try
            {
                await using var context = CreateContext(dbPath);

                await SqliteCommissionSchemaCompatibility.EnsureCommissionSchemaAsync(context, NullLogger.Instance);

                var tableNames = await ReadObjectNamesAsync(dbPath, "table");
                Assert.Contains("CommissionDeals", tableNames);
                Assert.Contains("CommissionLinks", tableNames);
                Assert.Contains("SaleRecords", tableNames);
                Assert.Contains("CommissionLedgers", tableNames);

                var indexNames = await ReadObjectNamesAsync(dbPath, "index");
                Assert.Contains("IX_CommissionLinks_SponsorId", indexNames);
                Assert.Contains("IX_SaleRecords_AccountId", indexNames);
                Assert.Contains("IX_SaleRecords_ImportBatchId", indexNames);
                Assert.Contains("IX_SaleRecords_SaleDate", indexNames);
                Assert.Contains("IX_CommissionLedgers_BeneficiaryId", indexNames);
                Assert.Contains("IX_CommissionLedgers_SaleRecordId", indexNames);
                Assert.Contains("IX_CommissionLedgers_SaleRecordId_BeneficiaryId", indexNames);
            }
            finally
            {
                DeleteSqliteFile(dbPath);
            }
        }

        [Fact]
        public async Task EnsureCommissionSchemaAsync_IsIdempotentForLegacySqliteDatabase()
        {
            var dbPath = await CreateLegacySqliteDatabaseAsync();

            try
            {
                await using var context = CreateContext(dbPath);

                await SqliteCommissionSchemaCompatibility.EnsureCommissionSchemaAsync(context, NullLogger.Instance);
                await SqliteCommissionSchemaCompatibility.EnsureCommissionSchemaAsync(context, NullLogger.Instance);

                var tableNames = await ReadObjectNamesAsync(dbPath, "table");
                Assert.Contains("CommissionLedgers", tableNames);
            }
            finally
            {
                DeleteSqliteFile(dbPath);
            }
        }

        [Fact]
        public async Task StartupSqlitePath_UpgradesLegacyDatabaseToCurrentCommissionControlPlaneSchema()
        {
            var dbPath = await CreateLegacySqliteDatabaseAsync();

            try
            {
                await using var context = CreateContext(dbPath);

                await SqliteCommissionSchemaCompatibility.EnsureCommissionSchemaAsync(context, NullLogger.Instance);

                var tableNames = await ReadObjectNamesAsync(dbPath, "table");
                Assert.Contains("BusinessAccounts", tableNames);
                Assert.Contains("CommissionAgreements", tableNames);
                Assert.Contains("CommissionAgreementRecipients", tableNames);
                Assert.Contains("ImportProfiles", tableNames);
                Assert.Contains("ImportBatches", tableNames);
                Assert.Contains("ImportRows", tableNames);
                Assert.Contains("SaleEvents", tableNames);
                Assert.Contains("CommissionLedgerEntries", tableNames);
                Assert.Contains("CommissionAdjustments", tableNames);
                Assert.Contains("PayoutBatches", tableNames);
                Assert.Contains("PayoutEntries", tableNames);
            }
            finally
            {
                DeleteSqliteFile(dbPath);
            }
        }

        [Fact]
        public async Task PersistentSqlitePath_WithSchemaDrift_ThrowsInsteadOfDeletingDatabase()
        {
            var dbPath = await CreateLegacySqliteDatabaseAsync(useEphemeralPath: false);

            try
            {
                await using var context = CreateContext(dbPath);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    SqliteCommissionSchemaCompatibility.EnsureCommissionSchemaAsync(context, NullLogger.Instance));

                Assert.Contains("Refusing to recreate a non-ephemeral SQLite database", ex.Message);

                var tableNames = await ReadObjectNamesAsync(dbPath, "table");
                Assert.Contains("AspNetUsers", tableNames);
                Assert.DoesNotContain("BusinessAccounts", tableNames);
            }
            finally
            {
                DeleteSqliteFile(dbPath);
            }
        }

        private static ApplicationDbContext CreateContext(string dbPath)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            return new ApplicationDbContext(options);
        }

        private static async Task<string> CreateLegacySqliteDatabaseAsync(bool useEphemeralPath = true)
        {
            var basePath = useEphemeralPath
                ? Path.GetTempPath()
                : Path.Combine(AppContext.BaseDirectory, "sqlite-schema-compat");
            Directory.CreateDirectory(basePath);

            var dbPath = Path.Combine(basePath, $"legacy-commission-{Guid.NewGuid():N}.db");

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA foreign_keys = ON;
                CREATE TABLE "AspNetUsers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY
                );
                """;
            await command.ExecuteNonQueryAsync();

            return dbPath;
        }

        private static async Task<HashSet<string>> ReadObjectNamesAsync(string dbPath, string type)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = $type;";
            command.Parameters.AddWithValue("$type", type);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }

        private static void DeleteSqliteFile(string dbPath)
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
