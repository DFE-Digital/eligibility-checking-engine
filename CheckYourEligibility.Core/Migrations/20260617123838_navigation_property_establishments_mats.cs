using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class navigation_property_establishments_mats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MultiAcademyTrustEstablishments_EstablishmentID",
                table: "MultiAcademyTrustEstablishments",
                column: "EstablishmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_MultiAcademyTrustEstablishments_Establishments_EstablishmentID",
                table: "MultiAcademyTrustEstablishments",
                column: "EstablishmentID",
                principalTable: "Establishments",
                principalColumn: "EstablishmentID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MultiAcademyTrustEstablishments_Establishments_EstablishmentID",
                table: "MultiAcademyTrustEstablishments");

            migrationBuilder.DropIndex(
                name: "IX_MultiAcademyTrustEstablishments_EstablishmentID",
                table: "MultiAcademyTrustEstablishments");
        }
    }
}
