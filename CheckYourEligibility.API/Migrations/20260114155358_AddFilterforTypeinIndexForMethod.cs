using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterforTypeinIndexForMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_Method",
                table: "Audits");

            migrationBuilder.CreateIndex(
                name: "idx_Method",
                table: "Audits",
                column: "Method",
                filter: "[Method] = 'POST' AND [Type] = 'Check'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_Method",
                table: "Audits");

            migrationBuilder.CreateIndex(
                name: "idx_Method",
                table: "Audits",
                column: "Method",
                filter: "[Method] = 'POST'");
        }
    }
}
