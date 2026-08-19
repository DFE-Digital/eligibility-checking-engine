using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class fosterFam_EligibilityCodeRange_tbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren");

            migrationBuilder.CreateTable(
                name: "EligibilityCodeRanges",
                columns: table => new
                {
                    EligibilityCodeRangeId = table.Column<int>(
                        type: "int",
                        nullable: false),
                    StartRange = table.Column<long>(
                        type: "bigint",
                        nullable: false),
                    EndRange = table.Column<long>(
                        type: "bigint",
                        nullable: false),
                    NextAvailableCode = table.Column<long>(
                        type: "bigint",
                        nullable: false),
                    RowVersion = table.Column<byte[]>(
                        type: "rowversion",
                        rowVersion: true,
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_EligibilityCodeRanges",
                        x => x.EligibilityCodeRangeId);
                });

            // Seed the mutable allocator state explicitly.
            // SQL Server will generate RowVersion.
            migrationBuilder.InsertData(
                table: "EligibilityCodeRanges",
                columns:
                [
                    "EligibilityCodeRangeId",
                    "StartRange",
                    "EndRange",
                    "NextAvailableCode"
                ],
                values:
                [
                    1,
                    40000000001L,
                    49999999999L,
                    40000000001L
                ]);

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren",
                column: "EligibilityCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCodeRanges");

            migrationBuilder.DropIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren");

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_EligibilityCode",
                table: "FosterChildren",
                column: "EligibilityCode");
        }
    }
}
