using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class applicationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_LocalAuthorities_LocalAuthorityId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_LocalAuthorityId",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Applications",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_ApplicationStatus",
                table: "Applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_ApplicationStatus",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_LocalAuthorityId",
                table: "Applications",
                column: "LocalAuthorityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_LocalAuthorities_LocalAuthorityId",
                table: "Applications",
                column: "LocalAuthorityId",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
