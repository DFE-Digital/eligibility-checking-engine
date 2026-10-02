using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingFamiliesDualRunningCheckIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkingFamiliesDualRunningChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EligibilityCheckID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EligibilityCode = table.Column<string>(type: "nchar(11)", nullable: false),
                    ECEStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECSStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECSQualifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isConflict = table.Column<bool>(type: "bit", nullable: false),
                    ECSValidityDates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECEValidityDates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingFamiliesDualRunningChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkingFamiliesDualRunningChecks_EligibilityCheck_EligibilityCheckID",
                        column: x => x.EligibilityCheckID,
                        principalTable: "EligibilityCheck",
                        principalColumn: "EligibilityCheckID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WFDualRunningChecks_Code_Conflict_Created",
                table: "WorkingFamiliesDualRunningChecks",
                columns: new[] { "EligibilityCode", "isConflict", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WFDualRunningChecks_Conflict_Created",
                table: "WorkingFamiliesDualRunningChecks",
                columns: new[] { "isConflict", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_WFDualRunningChecks_Created",
                table: "WorkingFamiliesDualRunningChecks",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingFamiliesDualRunningChecks_EligibilityCheckID",
                table: "WorkingFamiliesDualRunningChecks",
                column: "EligibilityCheckID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingFamiliesDualRunningChecks");
        }
    }
}
