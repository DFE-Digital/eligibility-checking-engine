using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameEstablishmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MultiAcademyTrustSchools",
                table: "MultiAcademyTrustSchools");

            migrationBuilder.RenameTable(
                name: "MultiAcademyTrustSchools",
                newName: "MultiAcademyTrustEstablishments");

            migrationBuilder.RenameIndex(
                name: "IX_MultiAcademyTrustSchools_MultiAcademyTrustID",
                table: "MultiAcademyTrustEstablishments",
                newName: "IX_MultiAcademyTrustEstablishments_MultiAcademyTrustID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MultiAcademyTrustEstablishments",
                table: "MultiAcademyTrustEstablishments",
                column: "MultiAcademyTrustEstablishmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_MultiAcademyTrustEstablishments_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustEstablishments",
                column: "MultiAcademyTrustID",
                principalTable: "MultiAcademyTrusts",
                principalColumn: "MultiAcademyTrustID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MultiAcademyTrustEstablishments_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustEstablishments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MultiAcademyTrustEstablishments",
                table: "MultiAcademyTrustEstablishments");

            migrationBuilder.RenameTable(
                name: "MultiAcademyTrustEstablishments",
                newName: "MultiAcademyTrustSchools");

            migrationBuilder.RenameIndex(
                name: "IX_MultiAcademyTrustEstablishments_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools",
                newName: "IX_MultiAcademyTrustSchools_MultiAcademyTrustID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MultiAcademyTrustSchools",
                table: "MultiAcademyTrustSchools",
                column: "MultiAcademyTrustEstablishmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools",
                column: "MultiAcademyTrustID",
                principalTable: "MultiAcademyTrusts",
                principalColumn: "MultiAcademyTrustID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
