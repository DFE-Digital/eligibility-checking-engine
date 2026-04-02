using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkingFamiliesEventSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkingFamiliesEventSummaries",
                columns: table => new
                {
                    WorkingFamiliesEventSummaryID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EligibilityCode = table.Column<string>(type: "nchar(11)", nullable: false),
                    OwningLocalAuthorityId = table.Column<int>(type: "int", nullable: false),
                    HasCodeBeenCheckedByOwningLA = table.Column<bool>(type: "bit", nullable: false),
                    ChildDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChildPostCode = table.Column<string>(type: "nvarchar(9)", nullable: true),
                    ChildFirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ChildFirstNameTruncated = table.Column<string>(type: "varchar(26)", nullable: false),
                    ParentNationalInsuranceNumber = table.Column<string>(type: "varchar(10)", nullable: true),
                    PartnerNationalInsuranceNumber = table.Column<string>(type: "varchar(10)", nullable: true),
                    ValidityStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GracePeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstCheckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstEventDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscretionaryValidityStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LatestSubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstCheckLocalAuthorityId = table.Column<int>(type: "int", nullable: true),
                    LastCheckLocalAuthorityId = table.Column<int>(type: "int", nullable: true),
                    Qualifier = table.Column<string>(type: "nvarchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingFamiliesEventSummaries", x => x.WorkingFamiliesEventSummaryID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingFamiliesEventSummaries_EligibilityCode",
                table: "WorkingFamiliesEventSummaries",
                column: "EligibilityCode");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingFamiliesEventSummaries_OwningLocalAuthorityId",
                table: "WorkingFamiliesEventSummaries",
                column: "OwningLocalAuthorityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingFamiliesEventSummaries");
        }
    }
}
