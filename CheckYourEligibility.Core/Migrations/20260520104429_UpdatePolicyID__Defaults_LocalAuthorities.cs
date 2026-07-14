using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePolicyID__Defaults_LocalAuthorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyYearsPupilPremiumPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FreeSchoolMealsPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TwoYearPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.DropColumn(
                name: "EarlyYearsPupilPremiumPolicyID",
                table: "LocalAuthorities");

            migrationBuilder.DropColumn(
                name: "FreeSchoolMealsPolicyID",
                table: "LocalAuthorities");

            migrationBuilder.DropColumn(
                name: "TwoYearPolicyID",
                table: "LocalAuthorities");
        }
    }
}
