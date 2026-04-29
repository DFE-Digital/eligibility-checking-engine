using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Remove_FosterChild_Relationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren");

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren",
                column: "FosterCarerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren");

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren",
                column: "FosterCarerId",
                unique: true);
        }
    }
}
