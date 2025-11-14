using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAuditTableNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "url",
                table: "Audits",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "typeId",
                table: "Audits",
                newName: "TypeId");

            migrationBuilder.RenameColumn(
                name: "source",
                table: "Audits",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "scope",
                table: "Audits",
                newName: "Scope");

            migrationBuilder.RenameColumn(
                name: "method",
                table: "Audits",
                newName: "Method");

            migrationBuilder.RenameColumn(
                name: "authentication",
                table: "Audits",
                newName: "Authentication");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Url",
                table: "Audits",
                newName: "url");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "Audits",
                newName: "typeId");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Audits",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "Scope",
                table: "Audits",
                newName: "scope");

            migrationBuilder.RenameColumn(
                name: "Method",
                table: "Audits",
                newName: "method");

            migrationBuilder.RenameColumn(
                name: "Authentication",
                table: "Audits",
                newName: "authentication");
        }
    }
}
