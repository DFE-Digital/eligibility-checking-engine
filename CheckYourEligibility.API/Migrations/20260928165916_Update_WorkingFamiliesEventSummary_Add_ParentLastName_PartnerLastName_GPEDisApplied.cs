using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_WorkingFamiliesEventSummary_Add_ParentLastName_PartnerLastName_GPEDisApplied : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DiscretionaryValidityStartDate",
                table: "WorkingFamiliesEventSummaries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GracePeriodEndDateApplied",
                table: "WorkingFamiliesEventSummaries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentLastName",
                table: "WorkingFamiliesEventSummaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerLastName",
                table: "WorkingFamiliesEventSummaries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodEndDateApplied",
                table: "WorkingFamiliesEventSummaries");

            migrationBuilder.DropColumn(
                name: "ParentLastName",
                table: "WorkingFamiliesEventSummaries");

            migrationBuilder.DropColumn(
                name: "PartnerLastName",
                table: "WorkingFamiliesEventSummaries");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DiscretionaryValidityStartDate",
                table: "WorkingFamiliesEventSummaries",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
