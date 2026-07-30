using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email_Reference",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email_Reference_UserType",
                table: "Users",
                columns: new[] { "Email", "Reference", "UserType" },
                unique: true,
                filter: "[UserType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName_OrganisationType_OrganisationId_UserType",
                table: "Users",
                columns: new[] { "UserName", "OrganisationType", "OrganisationId", "UserType" },
                unique: true,
                filter: "[UserName] IS NOT NULL AND [OrganisationType] IS NOT NULL AND [OrganisationId] IS NOT NULL AND [UserType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email_Reference_UserType",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName_OrganisationType_OrganisationId_UserType",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email_Reference",
                table: "Users",
                columns: new[] { "Email", "Reference" },
                unique: true);
        }
    }
}
