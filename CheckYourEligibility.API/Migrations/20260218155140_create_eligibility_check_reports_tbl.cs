using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class create_eligibility_check_reports_tbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EligibilityCheckReports",
                columns: table => new
                {
                    EligibilityCheckReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberOfResults = table.Column<int>(type: "int", nullable: false),
                    LocalAuthorityID = table.Column<int>(type: "int", nullable: true),
                    CheckType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCheckReports", x => x.EligibilityCheckReportId);
                    table.ForeignKey(
                        name: "FK_EligibilityCheckReports_LocalAuthorities_LocalAuthorityID",
                        column: x => x.LocalAuthorityID,
                        principalTable: "LocalAuthorities",
                        principalColumn: "LocalAuthorityID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheckReports_LocalAuthorityID",
                table: "EligibilityCheckReports",
                column: "LocalAuthorityID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCheckReports");
        }
    }
}
