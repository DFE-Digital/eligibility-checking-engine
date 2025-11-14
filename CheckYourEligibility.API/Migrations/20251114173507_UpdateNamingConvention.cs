using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityId",
                table: "BulkChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_Establishments_LocalAuthorities_LocalAuthorityId",
                table: "Establishments");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_TrustId",
                table: "MultiAcademyTrustSchools");

            migrationBuilder.RenameColumn(
                name: "WorkingFamiliesEventId",
                table: "WorkingFamiliesEvents",
                newName: "WorkingFamiliesEventID");

            migrationBuilder.RenameColumn(
                name: "RateLimitEventId",
                table: "RateLimitEvents",
                newName: "RateLimitEventID");

            migrationBuilder.RenameColumn(
                name: "TrustId",
                table: "MultiAcademyTrustSchools",
                newName: "MultiAcademyTrustID");

            migrationBuilder.RenameColumn(
                name: "SchoolId",
                table: "MultiAcademyTrustSchools",
                newName: "EstablishmentID");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "MultiAcademyTrustSchools",
                newName: "MultiAcademyTrustEstablishmentID");

            migrationBuilder.RenameIndex(
                name: "IX_MultiAcademyTrustSchools_TrustId",
                table: "MultiAcademyTrustSchools",
                newName: "IX_MultiAcademyTrustSchools_MultiAcademyTrustID");

            migrationBuilder.RenameColumn(
                name: "UID",
                table: "MultiAcademyTrusts",
                newName: "MultiAcademyTrustID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityId",
                table: "LocalAuthorities",
                newName: "LocalAuthorityID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityId",
                table: "Establishments",
                newName: "LocalAuthorityID");

            migrationBuilder.RenameColumn(
                name: "EstablishmentId",
                table: "Establishments",
                newName: "EstablishmentID");

            migrationBuilder.RenameIndex(
                name: "IX_Establishments_LocalAuthorityId",
                table: "Establishments",
                newName: "IX_Establishments_LocalAuthorityID");

            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                table: "ECSConflicts",
                newName: "CorrelationID");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "ECSConflicts",
                newName: "ECSConflictID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityId",
                table: "BulkChecks",
                newName: "LocalAuthorityID");

            migrationBuilder.RenameIndex(
                name: "IX_BulkChecks_LocalAuthorityId",
                table: "BulkChecks",
                newName: "IX_BulkChecks_LocalAuthorityID");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "Audits",
                newName: "TypeID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityId",
                table: "Applications",
                newName: "LocalAuthorityID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ApplicationEvidence",
                newName: "ApplicationEvidenceID");

            migrationBuilder.AddForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityID",
                table: "BulkChecks",
                column: "LocalAuthorityID",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityID");

            migrationBuilder.AddForeignKey(
                name: "FK_Establishments_LocalAuthorities_LocalAuthorityID",
                table: "Establishments",
                column: "LocalAuthorityID",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools",
                column: "MultiAcademyTrustID",
                principalTable: "MultiAcademyTrusts",
                principalColumn: "MultiAcademyTrustID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityID",
                table: "BulkChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_Establishments_LocalAuthorities_LocalAuthorityID",
                table: "Establishments");

            migrationBuilder.DropForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools");

            migrationBuilder.RenameColumn(
                name: "WorkingFamiliesEventID",
                table: "WorkingFamiliesEvents",
                newName: "WorkingFamiliesEventId");

            migrationBuilder.RenameColumn(
                name: "RateLimitEventID",
                table: "RateLimitEvents",
                newName: "RateLimitEventId");

            migrationBuilder.RenameColumn(
                name: "MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools",
                newName: "TrustId");

            migrationBuilder.RenameColumn(
                name: "EstablishmentID",
                table: "MultiAcademyTrustSchools",
                newName: "SchoolId");

            migrationBuilder.RenameColumn(
                name: "MultiAcademyTrustEstablishmentID",
                table: "MultiAcademyTrustSchools",
                newName: "ID");

            migrationBuilder.RenameIndex(
                name: "IX_MultiAcademyTrustSchools_MultiAcademyTrustID",
                table: "MultiAcademyTrustSchools",
                newName: "IX_MultiAcademyTrustSchools_TrustId");

            migrationBuilder.RenameColumn(
                name: "MultiAcademyTrustID",
                table: "MultiAcademyTrusts",
                newName: "UID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityID",
                table: "LocalAuthorities",
                newName: "LocalAuthorityId");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityID",
                table: "Establishments",
                newName: "LocalAuthorityId");

            migrationBuilder.RenameColumn(
                name: "EstablishmentID",
                table: "Establishments",
                newName: "EstablishmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Establishments_LocalAuthorityID",
                table: "Establishments",
                newName: "IX_Establishments_LocalAuthorityId");

            migrationBuilder.RenameColumn(
                name: "CorrelationID",
                table: "ECSConflicts",
                newName: "CorrelationId");

            migrationBuilder.RenameColumn(
                name: "ECSConflictID",
                table: "ECSConflicts",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityID",
                table: "BulkChecks",
                newName: "LocalAuthorityId");

            migrationBuilder.RenameIndex(
                name: "IX_BulkChecks_LocalAuthorityID",
                table: "BulkChecks",
                newName: "IX_BulkChecks_LocalAuthorityId");

            migrationBuilder.RenameColumn(
                name: "TypeID",
                table: "Audits",
                newName: "TypeId");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityID",
                table: "Applications",
                newName: "LocalAuthorityId");

            migrationBuilder.RenameColumn(
                name: "ApplicationEvidenceID",
                table: "ApplicationEvidence",
                newName: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BulkChecks_LocalAuthorities_LocalAuthorityId",
                table: "BulkChecks",
                column: "LocalAuthorityId",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Establishments_LocalAuthorities_LocalAuthorityId",
                table: "Establishments",
                column: "LocalAuthorityId",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_TrustId",
                table: "MultiAcademyTrustSchools",
                column: "TrustId",
                principalTable: "MultiAcademyTrusts",
                principalColumn: "UID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
