using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class Add_OrganisationType_OrganisationId_BulkChecktbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganisationID",
                table: "BulkChecks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganisationType",
                table: "BulkChecks",
                type: "nvarchar(20)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganisationID",
                table: "BulkChecks");

            migrationBuilder.DropColumn(
                name: "OrganisationType",
                table: "BulkChecks");
        }
    }
}
