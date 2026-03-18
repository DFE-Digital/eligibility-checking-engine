using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class WorkingFamiliesEventHMRCIdSoftDeleteAndAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateTime",
                table: "WorkingFamiliesEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDateTime",
                table: "WorkingFamiliesEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EventDateTime",
                table: "WorkingFamiliesEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HMRCEligibilityEventId",
                table: "WorkingFamiliesEvents",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkingFamiliesEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDateTime",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropColumn(
                name: "DeletedDateTime",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropColumn(
                name: "EventDateTime",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropColumn(
                name: "HMRCEligibilityEventId",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkingFamiliesEvents");
        }
    }
}
