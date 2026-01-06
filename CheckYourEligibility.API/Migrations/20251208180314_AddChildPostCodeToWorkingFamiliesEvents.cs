using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class AddChildPostCodeToWorkingFamiliesEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChildPostCode",
                table: "WorkingFamiliesEvents",
                type: "nvarchar(9)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChildPostCode",
                table: "WorkingFamiliesEvents");
        }
    }
}
