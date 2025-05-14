using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSequenceWithClientIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "EligibilityCheck");

            migrationBuilder.AddColumn<string>(
                name: "ClientIdentifier",
                table: "EligibilityCheck",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientIdentifier",
                table: "EligibilityCheck");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "EligibilityCheck",
                type: "int",
                nullable: true);
        }
    }
}
