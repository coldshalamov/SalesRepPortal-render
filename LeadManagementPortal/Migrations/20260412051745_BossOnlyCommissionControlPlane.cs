using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadManagementPortal.Migrations
{
    /// <inheritdoc />
    public partial class BossOnlyCommissionControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustments_AspNetUsers_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustments_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ColumnMappingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutBatches_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessAccountId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProductNameFilter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAgreements_BusinessAccounts_BusinessAccountId",
                        column: x => x.BusinessAccountId,
                        principalTable: "BusinessAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessAccountId = table.Column<int>(type: "int", nullable: false),
                    ExternalRowId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreditedRepId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleEvents_AspNetUsers_CreditedRepId",
                        column: x => x.CreditedRepId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleEvents_AspNetUsers_PostedById",
                        column: x => x.PostedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleEvents_BusinessAccounts_BusinessAccountId",
                        column: x => x.BusinessAccountId,
                        principalTable: "BusinessAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImportProfileId = table.Column<int>(type: "int", nullable: true),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportBatches_ImportProfiles_ImportProfileId",
                        column: x => x.ImportProfileId,
                        principalTable: "ImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommissionAgreementRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionAgreementId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    RateOrAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BasisRecipientId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAgreementRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAgreementRecipients_AspNetUsers_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAgreementRecipients_CommissionAgreementRecipients_BasisRecipientId",
                        column: x => x.BasisRecipientId,
                        principalTable: "CommissionAgreementRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAgreementRecipients_CommissionAgreements_CommissionAgreementId",
                        column: x => x.CommissionAgreementId,
                        principalTable: "CommissionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExternalRowId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BusinessAccountId = table.Column<int>(type: "int", nullable: true),
                    BusinessAccountExternalKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusinessAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SelectedAgreementId = table.Column<int>(type: "int", nullable: true),
                    SaleEventId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreditedRepId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MappedPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_AspNetUsers_CreditedRepId",
                        column: x => x.CreditedRepId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportRows_BusinessAccounts_BusinessAccountId",
                        column: x => x.BusinessAccountId,
                        principalTable: "BusinessAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportRows_CommissionAgreements_SelectedAgreementId",
                        column: x => x.SelectedAgreementId,
                        principalTable: "CommissionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportRows_SaleEvents_SaleEventId",
                        column: x => x.SaleEventId,
                        principalTable: "SaleEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommissionLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleEventId = table.Column<int>(type: "int", nullable: false),
                    CommissionAgreementId = table.Column<int>(type: "int", nullable: false),
                    CommissionAgreementRecipientId = table.Column<int>(type: "int", nullable: true),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    CalculationDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EarnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_AspNetUsers_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_CommissionAgreementRecipients_CommissionAgreementRecipientId",
                        column: x => x.CommissionAgreementRecipientId,
                        principalTable: "CommissionAgreementRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_CommissionAgreements_CommissionAgreementId",
                        column: x => x.CommissionAgreementId,
                        principalTable: "CommissionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_SaleEvents_SaleEventId",
                        column: x => x.SaleEventId,
                        principalTable: "SaleEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayoutEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayoutBatchId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CommissionLedgerEntryId = table.Column<int>(type: "int", nullable: true),
                    CommissionAdjustmentId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutEntries", x => x.Id);
                    table.CheckConstraint("CK_PayoutEntries_SourceSelection", "(([CommissionLedgerEntryId] IS NOT NULL AND [CommissionAdjustmentId] IS NULL) OR ([CommissionLedgerEntryId] IS NULL AND [CommissionAdjustmentId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_PayoutEntries_AspNetUsers_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutEntries_CommissionAdjustments_CommissionAdjustmentId",
                        column: x => x.CommissionAdjustmentId,
                        principalTable: "CommissionAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutEntries_CommissionLedgerEntries_CommissionLedgerEntryId",
                        column: x => x.CommissionLedgerEntryId,
                        principalTable: "CommissionLedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutEntries_PayoutBatches_PayoutBatchId",
                        column: x => x.PayoutBatchId,
                        principalTable: "PayoutBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAccounts_ExternalKey",
                table: "BusinessAccounts",
                column: "ExternalKey");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAccounts_Name",
                table: "BusinessAccounts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustments_BeneficiaryId",
                table: "CommissionAdjustments",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustments_CreatedAtUtc",
                table: "CommissionAdjustments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustments_CreatedById",
                table: "CommissionAdjustments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAgreementRecipients_BasisRecipientId",
                table: "CommissionAgreementRecipients",
                column: "BasisRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAgreementRecipients_BeneficiaryId",
                table: "CommissionAgreementRecipients",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAgreementRecipients_CommissionAgreementId",
                table: "CommissionAgreementRecipients",
                column: "CommissionAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAgreements_BusinessAccountId_IsActive_EffectiveStartDate_EffectiveEndDate",
                table: "CommissionAgreements",
                columns: new[] { "BusinessAccountId", "IsActive", "EffectiveStartDate", "EffectiveEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_BeneficiaryId",
                table: "CommissionLedgerEntries",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_CommissionAgreementId",
                table: "CommissionLedgerEntries",
                column: "CommissionAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_CommissionAgreementRecipientId",
                table: "CommissionLedgerEntries",
                column: "CommissionAgreementRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_EarnedAtUtc",
                table: "CommissionLedgerEntries",
                column: "EarnedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_SaleEventId",
                table: "CommissionLedgerEntries",
                column: "SaleEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ImportProfileId",
                table: "ImportBatches",
                column: "ImportProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ReceivedAtUtc",
                table: "ImportBatches",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_Status",
                table: "ImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_UploadedById",
                table: "ImportBatches",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfiles_Name_IsActive",
                table: "ImportProfiles",
                columns: new[] { "Name", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_BusinessAccountId",
                table: "ImportRows",
                column: "BusinessAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_CreditedRepId",
                table: "ImportRows",
                column: "CreditedRepId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportBatchId_RowNumber",
                table: "ImportRows",
                columns: new[] { "ImportBatchId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_SaleEventId",
                table: "ImportRows",
                column: "SaleEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_SelectedAgreementId",
                table: "ImportRows",
                column: "SelectedAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_Status",
                table: "ImportRows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBatches_CreatedById",
                table: "PayoutBatches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBatches_PaidAtUtc",
                table: "PayoutBatches",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBatches_Reference",
                table: "PayoutBatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntries_BeneficiaryId",
                table: "PayoutEntries",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntries_CommissionAdjustmentId",
                table: "PayoutEntries",
                column: "CommissionAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntries_CommissionLedgerEntryId",
                table: "PayoutEntries",
                column: "CommissionLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutEntries_PayoutBatchId",
                table: "PayoutEntries",
                column: "PayoutBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_BusinessAccountId",
                table: "SaleEvents",
                column: "BusinessAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_CreditedRepId",
                table: "SaleEvents",
                column: "CreditedRepId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_PostedById",
                table: "SaleEvents",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_SaleDate",
                table: "SaleEvents",
                column: "SaleDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportRows");

            migrationBuilder.DropTable(
                name: "PayoutEntries");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "CommissionAdjustments");

            migrationBuilder.DropTable(
                name: "CommissionLedgerEntries");

            migrationBuilder.DropTable(
                name: "PayoutBatches");

            migrationBuilder.DropTable(
                name: "ImportProfiles");

            migrationBuilder.DropTable(
                name: "CommissionAgreementRecipients");

            migrationBuilder.DropTable(
                name: "SaleEvents");

            migrationBuilder.DropTable(
                name: "CommissionAgreements");

            migrationBuilder.DropTable(
                name: "BusinessAccounts");
        }
    }
}
