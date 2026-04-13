using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadManagementPortal.Data
{
    public static class SqliteCommissionSchemaCompatibility
    {
        private static readonly string[] RequiredTables =
        {
            "CommissionDeals",
            "CommissionLinks",
            "SaleRecords",
            "CommissionLedgers"
        };

        private static readonly string[] CompatibilityStatements =
        {
            "PRAGMA foreign_keys = ON;",
            """
            CREATE TABLE IF NOT EXISTS "CommissionDeals" (
                "ApplicationUserId" TEXT NOT NULL CONSTRAINT "PK_CommissionDeals" PRIMARY KEY,
                "DealType" INTEGER NOT NULL,
                "Rate" TEXT NOT NULL,
                "BaseCost" TEXT NULL,
                "CalculationBasis" INTEGER NOT NULL,
                CONSTRAINT "FK_CommissionDeals_AspNetUsers_ApplicationUserId" FOREIGN KEY ("ApplicationUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "CommissionLinks" (
                "DownlineId" TEXT NOT NULL CONSTRAINT "PK_CommissionLinks" PRIMARY KEY,
                "SponsorId" TEXT NOT NULL,
                CONSTRAINT "CK_CommissionLinks_NoSelfSponsor" CHECK ([DownlineId] <> [SponsorId]),
                CONSTRAINT "FK_CommissionLinks_AspNetUsers_DownlineId" FOREIGN KEY ("DownlineId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CommissionLinks_AspNetUsers_SponsorId" FOREIGN KEY ("SponsorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "SaleRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SaleRecords" PRIMARY KEY AUTOINCREMENT,
                "AccountId" TEXT NOT NULL,
                "ProductName" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL,
                "GrossAmount" TEXT NOT NULL,
                "CostAmount" TEXT NULL,
                "SaleDate" TEXT NOT NULL,
                "ImportBatchId" TEXT NOT NULL,
                "ImportedAt" TEXT NOT NULL,
                "RawPayload" TEXT NOT NULL,
                CONSTRAINT "FK_SaleRecords_AspNetUsers_AccountId" FOREIGN KEY ("AccountId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS "CommissionLedgers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CommissionLedgers" PRIMARY KEY AUTOINCREMENT,
                "SaleRecordId" INTEGER NOT NULL,
                "BeneficiaryId" TEXT NOT NULL,
                "GrossAmount" TEXT NOT NULL,
                "NetAmount" TEXT NOT NULL,
                "CommissionAmount" TEXT NOT NULL,
                "ChainDepth" INTEGER NOT NULL,
                "DealSnapshot" TEXT NOT NULL,
                "CalculationNotes" TEXT NOT NULL,
                CONSTRAINT "FK_CommissionLedgers_AspNetUsers_BeneficiaryId" FOREIGN KEY ("BeneficiaryId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_CommissionLedgers_SaleRecords_SaleRecordId" FOREIGN KEY ("SaleRecordId") REFERENCES "SaleRecords" ("Id") ON DELETE CASCADE
            );
            """,
            """CREATE INDEX IF NOT EXISTS "IX_CommissionLinks_SponsorId" ON "CommissionLinks" ("SponsorId");""",
            """CREATE INDEX IF NOT EXISTS "IX_SaleRecords_AccountId" ON "SaleRecords" ("AccountId");""",
            """CREATE INDEX IF NOT EXISTS "IX_SaleRecords_ImportBatchId" ON "SaleRecords" ("ImportBatchId");""",
            """CREATE INDEX IF NOT EXISTS "IX_SaleRecords_SaleDate" ON "SaleRecords" ("SaleDate");""",
            """CREATE INDEX IF NOT EXISTS "IX_CommissionLedgers_BeneficiaryId" ON "CommissionLedgers" ("BeneficiaryId");""",
            """CREATE INDEX IF NOT EXISTS "IX_CommissionLedgers_SaleRecordId" ON "CommissionLedgers" ("SaleRecordId");""",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CommissionLedgers_SaleRecordId_BeneficiaryId" ON "CommissionLedgers" ("SaleRecordId", "BeneficiaryId");"""
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

            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var existingTables = await ReadExistingTablesAsync(connection, cancellationToken);
                var missingTables = RequiredTables
                    .Where(table => !existingTables.Contains(table))
                    .ToArray();

                if (missingTables.Length == 0)
                {
                    return;
                }

                logger.LogWarning(
                    "SQLite database is missing commission schema tables: {MissingTables}. Applying compatibility DDL.",
                    string.Join(", ", missingTables));

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                foreach (var statement in CompatibilityStatements)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static async Task<HashSet<string>> ReadExistingTablesAsync(
            System.Data.Common.DbConnection connection,
            CancellationToken cancellationToken)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            return tables;
        }
    }
}
