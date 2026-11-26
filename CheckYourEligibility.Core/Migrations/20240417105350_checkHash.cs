using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class checkHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EligibilityCheckHashes",
                columns: table => new
                {
                    EligibilityCheckHashID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Hash = table.Column<string>(type: "varchar(5000)", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCheckHashes", x => x.EligibilityCheckHashID);
                });

            migrationBuilder.CreateIndex(
                name: "idx_EligibilityCheckHash",
                table: "EligibilityCheckHashes",
                column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCheckHashes");
        }
    }
}
