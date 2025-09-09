using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalAuthorityIdToBulkCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocalAuthorityId",
                table: "BulkChecks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BulkChecks_LocalAuthorityId",
                table: "BulkChecks",
                column: "LocalAuthorityId");

            migrationBuilder.AddForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityId",
                table: "BulkChecks",
                column: "LocalAuthorityId",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityId",
                table: "BulkChecks");

            migrationBuilder.DropIndex(
                name: "IX_BulkChecks_LocalAuthorityId",
                table: "BulkChecks");

            migrationBuilder.DropColumn(
                name: "LocalAuthorityId",
                table: "BulkChecks");
        }
    }
}
