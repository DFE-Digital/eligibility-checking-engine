using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class ECS_Conflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashId",
                table: "ECSConflicts");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityCheckHashID",
                table: "ECSConflicts",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ECSConflicts_EligibilityCheckHashID",
                table: "ECSConflicts",
                column: "EligibilityCheckHashID");

            migrationBuilder.AddForeignKey(
                name: "FK_ECSConflicts_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "ECSConflicts",
                column: "EligibilityCheckHashID",
                principalTable: "EligibilityCheckHashes",
                principalColumn: "EligibilityCheckHashID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ECSConflicts_EligibilityCheckHashes_EligibilityCheckHashID",
                table: "ECSConflicts");

            migrationBuilder.DropIndex(
                name: "IX_ECSConflicts_EligibilityCheckHashID",
                table: "ECSConflicts");

            migrationBuilder.DropColumn(
                name: "EligibilityCheckHashID",
                table: "ECSConflicts");

            migrationBuilder.AddColumn<string>(
                name: "HashId",
                table: "ECSConflicts",
                type: "varchar(MAX)",
                nullable: false,
                defaultValue: "");
        }
    }
}
