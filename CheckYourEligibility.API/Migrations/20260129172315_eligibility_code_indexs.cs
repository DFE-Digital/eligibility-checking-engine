using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class eligibility_code_indexs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidityStartDate",
                table: "FosterChildren",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidityEndDate",
                table: "FosterChildren",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmissionDate",
                table: "FosterChildren",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "FosterChildren",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityCode",
                table: "FosterChildren",
                type: "nchar(11)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PartnerDateOfBirth",
                table: "FosterCarers",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "FosterCarers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FosterChildren_EligibilityCode",
                table: "FosterChildren",
                column: "EligibilityCode");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingFamiliesEvents_EligibilityCode",
                table: "WorkingFamiliesEvents",
                column: "EligibilityCode");

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren",
                column: "EligibilityCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkingFamiliesEvents_EligibilityCode",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FosterChildren_EligibilityCode",
                table: "FosterChildren");

            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren");

            migrationBuilder.DropColumn(
                name: "EligibilityCode",
                table: "FosterChildren");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ValidityStartDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ValidityEndDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "SubmissionDate",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "FosterChildren",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PartnerDateOfBirth",
                table: "FosterCarers",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "FosterCarers",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
