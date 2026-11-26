using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class BaseMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EligibilityCheck",
                columns: table => new
                {
                    EligibilityCheckID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", nullable: false),
                    Status = table.Column<string>(type: "varchar(100)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NINumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    NASSNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityCheck", x => x.EligibilityCheckID);
                });

            migrationBuilder.CreateTable(
                name: "FreeSchoolMealsHMRC",
                columns: table => new
                {
                    FreeSchoolMealsHMRCID = table.Column<string>(type: "varchar(50)", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Surname = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeSchoolMealsHMRC", x => x.FreeSchoolMealsHMRCID);
                });

            migrationBuilder.CreateTable(
                name: "FreeSchoolMealsHO",
                columns: table => new
                {
                    FreeSchoolMealsHOID = table.Column<string>(type: "varchar(100)", nullable: false),
                    NASS = table.Column<string>(type: "varchar(50)", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeSchoolMealsHO", x => x.FreeSchoolMealsHOID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EligibilityCheck");

            migrationBuilder.DropTable(
                name: "FreeSchoolMealsHMRC");

            migrationBuilder.DropTable(
                name: "FreeSchoolMealsHO");
        }
    }
}
