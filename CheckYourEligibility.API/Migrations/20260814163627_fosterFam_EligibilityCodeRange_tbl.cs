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
            migrationBuilder.CreateTable(
                name: "EligibilityCodeRanges",
                columns: table => new
                {
                    EligibilityCodeRangeId = table.Column<int>(type: "int", nullable: false),
                    StartRange = table.Column<long>(type: "bigint", nullable: false),
                    EndRange = table.Column<long>(type: "bigint", nullable: false),
                    NextAvailableCode = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCodeRanges", x => x.EligibilityCodeRangeId);
                });

            migrationBuilder.InsertData(
                table: "EligibilityCodeRanges",
                columns: new[] { "EligibilityCodeRangeId", "EndRange", "NextAvailableCode", "StartRange" },
                values: new object[] { 1, 49999999999L, 40000000001L, 40000000001L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCodeRanges");
        }
    }
}
