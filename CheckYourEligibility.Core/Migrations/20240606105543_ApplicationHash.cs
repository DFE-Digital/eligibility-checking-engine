using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibilityCheckHashID",
                table: "Applications",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_EligibilityCheckHashID",
                table: "Applications",
                column: "EligibilityCheckHashID");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "Applications",
                column: "EligibilityCheckHashID",
                principalTable: "EligibilityCheckHashes",
                principalColumn: "EligibilityCheckHashID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_EligibilityCheckHashID",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EligibilityCheckHashID",
                table: "Applications");
        }
    }
}
