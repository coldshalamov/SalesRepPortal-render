using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadManagementPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrgToLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesOrgId",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SalesOrgId",
                table: "Leads",
                column: "SalesOrgId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_SalesOrgs_SalesOrgId",
                table: "Leads",
                column: "SalesOrgId",
                principalTable: "SalesOrgs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_SalesOrgs_SalesOrgId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_SalesOrgId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SalesOrgId",
                table: "Leads");
        }
    }
}
