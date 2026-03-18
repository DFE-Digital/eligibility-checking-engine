using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class IndexWorkingFamiliesEventsHMRCEligibilityEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_WorkingFamiliesEvents_HMRCEligibilityEventId",
                table: "WorkingFamiliesEvents",
                column: "HMRCEligibilityEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_WorkingFamiliesEvents_HMRCEligibilityEventId",
                table: "WorkingFamiliesEvents");
        }
    }
}
