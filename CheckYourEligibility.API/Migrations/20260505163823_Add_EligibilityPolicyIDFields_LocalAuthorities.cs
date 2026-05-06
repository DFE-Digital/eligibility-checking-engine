using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_EligibilityPolicyIDFields_LocalAuthorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyYearsPupilPremiumPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "FreeSchoolMealsPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TwoYearPolicyID",
                table: "LocalAuthorities",
                type: "int",
                nullable: false,
                defaultValue: 3);
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
