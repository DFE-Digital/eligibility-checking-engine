using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddEstablishmentSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Establishments_LocalAuthorityID",
                table: "Establishments");

            migrationBuilder.AlterColumn<string>(
                name: "EstablishmentName",
                table: "Establishments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "idx_Establishment_EstablishmentName",
                table: "Establishments",
                column: "EstablishmentName");

            migrationBuilder.CreateIndex(
                name: "idx_Establishment_LocalAuthorityID_EstablishmentName",
                table: "Establishments",
                columns: new[] { "LocalAuthorityID", "EstablishmentName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_Establishment_EstablishmentName",
                table: "Establishments");

            migrationBuilder.DropIndex(
                name: "idx_Establishment_LocalAuthorityID_EstablishmentName",
                table: "Establishments");

            migrationBuilder.AlterColumn<string>(
                name: "EstablishmentName",
                table: "Establishments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_LocalAuthorityID",
                table: "Establishments",
                column: "LocalAuthorityID");
        }
    }
}
