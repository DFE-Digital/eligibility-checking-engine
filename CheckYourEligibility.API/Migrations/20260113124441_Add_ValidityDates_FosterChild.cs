using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_ValidityDates_FosterChild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SubmissionDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidityEndDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidityStartDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "LocalAuthorityId",
                table: "FosterCarers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionDate",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "ValidityEndDate",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "ValidityStartDate",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "LocalAuthorityId",
                table: "FosterCarers");
        }
    }
}
