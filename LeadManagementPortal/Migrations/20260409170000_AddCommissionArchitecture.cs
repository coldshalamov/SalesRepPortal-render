using System;
using LeadManagementPortal.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadManagementPortal.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260409170000_AddCommissionArchitecture")]
    public partial class AddCommissionArchitecture : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionDeals",
                columns: table => new
                {
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DealType = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationBasis = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionDeals", x => x.ApplicationUserId);
                    table.ForeignKey(
                        name: "FK_CommissionDeals_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionLinks",
                columns: table => new
                {
                    DownlineId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SponsorId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionLinks", x => x.DownlineId);
                    table.CheckConstraint("CK_CommissionLinks_NoSelfSponsor", "[DownlineId] <> [SponsorId]");
                    table.ForeignKey(
                        name: "FK_CommissionLinks_AspNetUsers_DownlineId",
                        column: x => x.DownlineId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommissionLinks_AspNetUsers_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportBatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleRecords_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleRecordId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChainDepth = table.Column<int>(type: "int", nullable: false),
                    DealSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculationNotes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionLedgers_AspNetUsers_BeneficiaryId",
                        column: x => x.BeneficiaryId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionLedgers_SaleRecords_SaleRecordId",
                        column: x => x.SaleRecordId,
                        principalTable: "SaleRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_BeneficiaryId",
                table: "CommissionLedgers",
                column: "BeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_SaleRecordId",
                table: "CommissionLedgers",
                column: "SaleRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgers_SaleRecordId_BeneficiaryId",
                table: "CommissionLedgers",
                columns: new[] { "SaleRecordId", "BeneficiaryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLinks_SponsorId",
                table: "CommissionLinks",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_AccountId",
                table: "SaleRecords",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_ImportBatchId",
                table: "SaleRecords",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_SaleDate",
                table: "SaleRecords",
                column: "SaleDate");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionDeals");

            migrationBuilder.DropTable(
                name: "CommissionLedgers");

            migrationBuilder.DropTable(
                name: "CommissionLinks");

            migrationBuilder.DropTable(
                name: "SaleRecords");
        }
    }
}
