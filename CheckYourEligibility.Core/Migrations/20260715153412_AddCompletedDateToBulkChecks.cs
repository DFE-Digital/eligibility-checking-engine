using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedDateToBulkChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedDate",
                table: "BulkChecks",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "BulkChecks");
        }
    }
}
