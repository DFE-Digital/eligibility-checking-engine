using CheckYourEligibility.API.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_TierToEligibilityCheckLikeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "EligibilityCheckHashes",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "EligibilityCheck",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "Applications",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: EligibilityTier.Targeted);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tier",
                table: "EligibilityCheckHashes");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Applications");
        }
    }
}
