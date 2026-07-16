using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Create_CAPIAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CAPIAudits",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DWPCorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EligibilityCheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(210)", nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseCode = table.Column<int>(type: "int", nullable: false),
                    CAPIResponseCode = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAPIAudits", x => x.AuditId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CAPIAudits_DWPCorrelationId",
                table: "CAPIAudits",
                column: "DWPCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CAPIAudits_EligibilityCheckId",
                table: "CAPIAudits",
                column: "EligibilityCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_CAPIAudits_TimeStamp",
                table: "CAPIAudits",
                column: "TimeStamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CAPIAudits");
        }
    }
}
