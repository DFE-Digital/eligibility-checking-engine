using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewColumnsToEligibilityCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganisationID",
                table: "EligibilityCheck",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganisationType",
                table: "EligibilityCheck",
                type: "nvarchar(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "EligibilityCheck",
                type: "varchar(25)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "EligibilityCheck",
                type: "varchar(254)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganisationID",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "OrganisationType",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "EligibilityCheck");
        }
    }
}
