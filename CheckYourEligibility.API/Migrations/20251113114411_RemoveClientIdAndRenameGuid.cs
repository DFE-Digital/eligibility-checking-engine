using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClientIdAndRenameGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientIdentifier",
                table: "BulkChecks");

            migrationBuilder.RenameColumn(
                name: "Guid",
                table: "BulkChecks",
                newName: "BulkCheckID");

            migrationBuilder.RenameColumn(
                name: "Group",
                table: "EligibilityCheck",
                newName: "BulkCheckID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "BulkChecks",
                newName: "Guid");

            migrationBuilder.AddColumn<string>(
                name: "ClientIdentifier",
                table: "BulkChecks",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameColumn(
                name: "BulkCheckID",
                table: "EligibilityCheck",
                newName: "Group");
        }
    }
}
