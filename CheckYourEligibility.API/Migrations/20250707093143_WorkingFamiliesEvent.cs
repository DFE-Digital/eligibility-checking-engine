using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class WorkingFamiliesEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkingFamiliesEvents",
                columns: table => new
                {
                    WorkingFamiliesEventId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EligibilityCode = table.Column<string>(type: "nchar(11)", nullable: false),
                    ChildFirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ChildLastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ChildDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParentFirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ParentLastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ParentNationalInsuranceNumber = table.Column<string>(type: "varchar(10)", nullable: true),
                    PartnerFirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    PartnerLastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    PartnerNationalInsuranceNumber = table.Column<string>(type: "varchar(10)", nullable: true),
                    ValidityStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscretionaryValidityStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GracePeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingFamiliesEvents", x => x.WorkingFamiliesEventId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingFamiliesEvents");
        }
    }
}
