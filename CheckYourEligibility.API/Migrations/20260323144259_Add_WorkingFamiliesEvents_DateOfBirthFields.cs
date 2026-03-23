using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkingFamiliesEvents_DateOfBirthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ParentDateOfBirth",
                table: "WorkingFamiliesEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartnerDateOfBirth",
                table: "WorkingFamiliesEvents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentDateOfBirth",
                table: "WorkingFamiliesEvents");

            migrationBuilder.DropColumn(
                name: "PartnerDateOfBirth",
                table: "WorkingFamiliesEvents");
        }
    }
}
