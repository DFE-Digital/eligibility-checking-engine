using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class recreate_foster_family_tables_with_guids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing tables if they exist (from previous migrations)
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[FosterChildren]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[FosterChildren];
                
                IF OBJECT_ID(N'[dbo].[FosterCarers]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[FosterCarers];
            ");

            migrationBuilder.CreateTable(
                name: "FosterCarers",
                columns: table => new
                {
                    FosterCarerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    NationalInsuranceNumber = table.Column<string>(type: "varchar(50)", nullable: false),
                    HasPartner = table.Column<bool>(type: "bit", nullable: false),
                    PartnerFirstName = table.Column<string>(type: "varchar(100)", nullable: true),
                    PartnerLastName = table.Column<string>(type: "varchar(100)", nullable: true),
                    PartnerDateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    PartnerNationalInsuranceNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    LocalAuthorityID = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterCarers", x => x.FosterCarerId);
                    table.ForeignKey(
                        name: "FK_FosterCarers_LocalAuthorities_LocalAuthorityID",
                        column: x => x.LocalAuthorityID,
                        principalTable: "LocalAuthorities",
                        principalColumn: "LocalAuthorityID",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "FosterChildren",
                columns: table => new
                {
                    FosterChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    LastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    PostCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidityStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidityEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmissionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FosterCarerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FosterChildren", x => x.FosterChildId);
                    table.ForeignKey(
                        name: "FK_FosterChildren_FosterCarers_FosterCarerId",
                        column: x => x.FosterCarerId,
                        principalTable: "FosterCarers",
                        principalColumn: "FosterCarerId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FosterChildren_FosterCarerId",
                table: "FosterChildren",
                column: "FosterCarerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FosterCarers_LocalAuthorityID",
                table: "FosterCarers",
                column: "LocalAuthorityID");
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