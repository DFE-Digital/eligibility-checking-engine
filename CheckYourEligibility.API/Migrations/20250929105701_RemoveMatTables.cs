using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MultiAcademyTrustSchools");

            migrationBuilder.DropTable(
                name: "MultiAcademyTrusts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MultiAcademyTrusts",
                columns: table => new
                {
                    UID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiAcademyTrusts", x => x.UID);
                });

            migrationBuilder.CreateTable(
                name: "MultiAcademyTrustSchools",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrustId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiAcademyTrustSchools", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MultiAcademyTrustSchools_MultiAcademyTrusts_TrustId",
                        column: x => x.TrustId,
                        principalTable: "MultiAcademyTrusts",
                        principalColumn: "UID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MultiAcademyTrustSchools_TrustId",
                table: "MultiAcademyTrustSchools",
                column: "TrustId");
        }
    }
}
