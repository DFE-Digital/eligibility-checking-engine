using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class DeCoupleCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "NASSNumber",
                table: "EligibilityCheck");

            migrationBuilder.DropColumn(
                name: "NINumber",
                table: "EligibilityCheck");

            migrationBuilder.AddColumn<string>(
                name: "CheckData",
                table: "EligibilityCheck",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckData",
                table: "EligibilityCheck");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "EligibilityCheck",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "EligibilityCheck",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NASSNumber",
                table: "EligibilityCheck",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NINumber",
                table: "EligibilityCheck",
                type: "varchar(50)",
                nullable: true);
        }
    }
}
