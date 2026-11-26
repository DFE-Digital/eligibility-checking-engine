using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class BulkUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "EligibilityCheck",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                table: "EligibilityCheck");
        }
    }
}
