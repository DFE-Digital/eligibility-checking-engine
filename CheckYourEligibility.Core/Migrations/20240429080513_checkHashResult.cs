using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class checkHashResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibilityCheckHashID",
                table: "EligibilityCheck",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheck_EligibilityCheckHashID",
                table: "EligibilityCheck",
                column: "EligibilityCheckHashID");

            migrationBuilder.AddForeignKey(
                name: "FK_EligibilityCheck_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "EligibilityCheck",
                column: "EligibilityCheckHashID",
                principalTable: "EligibilityCheckHashes",
                principalColumn: "EligibilityCheckHashID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EligibilityCheck_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "EligibilityCheck");

            migrationBuilder.DropIndex(
                name: "IX_EligibilityCheck_EligibilityCheckHashID",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "EligibilityCheckHashID",
                table: "EligibilityCheck");
        }
    }
}
