using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class renametbl_EligibilityCheckReportItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCheckReportItems");

            migrationBuilder.CreateTable(
                name: "EligibilityCheckReportItem",
                columns: table => new
                {
                    EligibilityCheckReportItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EligibilityCheckReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EligibilityCheckID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsBulkCheckItem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCheckReportItem", x => x.EligibilityCheckReportItemId);
                    table.ForeignKey(
                        name: "FK_EligibilityCheckReportItem_EligibilityCheckReports_EligibilityCheckReportId",
                        column: x => x.EligibilityCheckReportId,
                        principalTable: "EligibilityCheckReports",
                        principalColumn: "EligibilityCheckReportId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EligibilityCheckReportItem_EligibilityCheck_EligibilityCheckID",
                        column: x => x.EligibilityCheckID,
                        principalTable: "EligibilityCheck",
                        principalColumn: "EligibilityCheckID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheckReportItem_EligibilityCheckID",
                table: "EligibilityCheckReportItem",
                column: "EligibilityCheckID");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheckReportItem_EligibilityCheckReportId",
                table: "EligibilityCheckReportItem",
                column: "EligibilityCheckReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCheckReportItem");

            migrationBuilder.CreateTable(
                name: "EligibilityCheckReportItems",
                columns: table => new
                {
                    EligibilityCheckReportItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EligibilityCheckID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EligibilityCheckReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsBulkCheckItem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCheckReportItems", x => x.EligibilityCheckReportItemId);
                    table.ForeignKey(
                        name: "FK_EligibilityCheckReportItems_EligibilityCheckReports_EligibilityCheckReportId",
                        column: x => x.EligibilityCheckReportId,
                        principalTable: "EligibilityCheckReports",
                        principalColumn: "EligibilityCheckReportId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EligibilityCheckReportItems_EligibilityCheck_EligibilityCheckID",
                        column: x => x.EligibilityCheckID,
                        principalTable: "EligibilityCheck",
                        principalColumn: "EligibilityCheckID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheckReportItems_EligibilityCheckID",
                table: "EligibilityCheckReportItems",
                column: "EligibilityCheckID");

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCheckReportItems_EligibilityCheckReportId",
                table: "EligibilityCheckReportItems",
                column: "EligibilityCheckReportId");
        }
    }
}
