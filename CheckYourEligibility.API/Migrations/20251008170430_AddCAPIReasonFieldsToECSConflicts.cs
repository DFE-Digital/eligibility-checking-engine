using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCAPIReasonFieldsToECSConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CAPIEndpoint",
                table: "ECSConflicts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CAPIResponseCode",
                table: "ECSConflicts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ECSConflicts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CAPIEndpoint",
                table: "ECSConflicts");

            migrationBuilder.DropColumn(
                name: "CAPIResponseCode",
                table: "ECSConflicts");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ECSConflicts");
        }
    }
}
