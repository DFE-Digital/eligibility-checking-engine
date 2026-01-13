using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_FosterFamily_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FosterCarers",
                columns: table => new
                {
                    FosterCarerId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    NationalInsuranceNumber = table.Column<string>(type: "varchar(50)", nullable: false),
                    HasPartner = table.Column<bool>(type: "bit", nullable: false),
                    PartnerFirstName = table.Column<string>(type: "varchar(100)", nullable: true),
                    PartnerLastName = table.Column<string>(type: "varchar(100)", nullable: true),
                    PartnerDateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    PartnerNationalInsuranceNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterCarers", x => x.FosterCarerId);
                });

            migrationBuilder.CreateTable(
                name: "FosterChildren",
                columns: table => new
                {
                    FosterChildId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    PostCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FosterCarerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterChildren", x => x.FosterChildId);
                    table.ForeignKey(
                        name: "FK_FosterChildren_FosterCarers_FosterCarerId",
                        column: x => x.FosterCarerId,
                        principalTable: "FosterCarers",
                        principalColumn: "FosterCarerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren",
                column: "FosterCarerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FosterChildren");

            migrationBuilder.DropTable(
                name: "FosterCarers");
        }
    }
}
