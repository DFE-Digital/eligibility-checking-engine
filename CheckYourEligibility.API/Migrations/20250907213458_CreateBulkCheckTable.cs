using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class CreateBulkCheckTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientIdentifier",
                table: "EligibilityCheck");

            migrationBuilder.AlterColumn<string>(
                name: "Group",
                table: "EligibilityCheck",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BulkChecks",
                columns: table => new
                {
                    Guid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClientIdentifier = table.Column<string>(type: "varchar(100)", nullable: false),
                    Filename = table.Column<string>(type: "varchar(255)", nullable: false),
                    EligibilityType = table.Column<string>(type: "varchar(100)", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedBy = table.Column<string>(type: "varchar(100)", nullable: false),
                    Status = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkChecks", x => x.Guid);
                });

            // Clear existing Group values that would cause foreign key constraint violations
            // since we're refactoring to use the new BulkCheck table structure
            migrationBuilder.Sql("UPDATE EligibilityCheck SET [Group] = NULL WHERE [Group] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheck_Group",
                table: "EligibilityCheck",
                column: "Group");

            migrationBuilder.AddForeignKey(
                name: "FK_EligibilityCheck_BulkChecks_Group",
                table: "EligibilityCheck",
                column: "Group",
                principalTable: "BulkChecks",
                principalColumn: "Guid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EligibilityCheck_BulkChecks_Group",
                table: "EligibilityCheck");

            migrationBuilder.DropTable(
                name: "BulkChecks");

            migrationBuilder.DropIndex(
                name: "IX_EligibilityCheck_Group",
                table: "EligibilityCheck");

            migrationBuilder.AlterColumn<string>(
                name: "Group",
                table: "EligibilityCheck",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientIdentifier",
                table: "EligibilityCheck",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
