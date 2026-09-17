using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_FosterChildRecord_Link_To_WorkingFamilyEventSummaryRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkingFamiliesEventSummaryID",
                table: "FosterChildren",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_WorkingFamiliesEventSummaryID",
                table: "FosterChildren",
                column: "WorkingFamiliesEventSummaryID");

            migrationBuilder.AddForeignKey(
                name: "FK_FosterChildren_WorkingFamiliesEventSummaries_WorkingFamiliesEventSummaryID",
                table: "FosterChildren",
                column: "WorkingFamiliesEventSummaryID",
                principalTable: "WorkingFamiliesEventSummaries",
                principalColumn: "WorkingFamiliesEventSummaryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FosterChildren_WorkingFamiliesEventSummaries_WorkingFamiliesEventSummaryID",
                table: "FosterChildren");

            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_WorkingFamiliesEventSummaryID",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "WorkingFamiliesEventSummaryID",
                table: "FosterChildren");
        }
    }
}
