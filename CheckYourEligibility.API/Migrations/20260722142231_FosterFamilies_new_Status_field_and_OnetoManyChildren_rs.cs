using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class FosterFamilies_new_Status_field_and_OnetoManyChildren_rs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FosterChildren",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FosterCarers",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FosterCarers");

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren",
                column: "FosterCarerId",
                unique: true);
        }
    }
}
