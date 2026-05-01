using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class dropstatus_fixid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "EligibilityCheckReports");

            migrationBuilder.RenameColumn(
                name: "EligibilityCheckReportItemsId",
                table: "EligibilityCheckReportItems",
                newName: "EligibilityCheckReportItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EligibilityCheckReportItemId",
                table: "EligibilityCheckReportItems",
                newName: "EligibilityCheckReportItemsId");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "EligibilityCheckReports",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
