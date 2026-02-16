using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadManagementPortal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SalesOrgs_SalesOrgId",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SalesOrgs_SalesOrgId",
                table: "AspNetUsers",
                column: "SalesOrgId",
                principalTable: "SalesOrgs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SalesOrgs_SalesOrgId",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SalesOrgs_SalesOrgId",
                table: "AspNetUsers",
                column: "SalesOrgId",
                principalTable: "SalesOrgs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
