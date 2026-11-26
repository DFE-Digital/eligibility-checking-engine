using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class establishmentImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalAuthorities",
                columns: table => new
                {
                    LocalAuthorityId = table.Column<int>(type: "int", nullable: false),
                    LaName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAuthorities", x => x.LocalAuthorityId);
                });

            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EstablishmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Locality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Town = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    County = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusOpen = table.Column<bool>(type: "bit", nullable: false),
                    LocalAuthorityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.SchoolId);
                    table.ForeignKey(
                        name: "FK_Schools_LocalAuthorities_LocalAuthorityId",
                        column: x => x.LocalAuthorityId,
                        principalTable: "LocalAuthorities",
                        principalColumn: "LocalAuthorityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    ApplicationID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", nullable: false),
                    Reference = table.Column<string>(type: "varchar(8)", nullable: false),
                    LocalAuthorityId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ParentFirstName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ParentLastName = table.Column<string>(type: "varchar(100)", nullable: false),
                    ParentNationalInsuranceNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    ParentNationalAsylumSeekerServiceNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    ParentDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChildFirstName = table.Column<string>(type: "varchar(50)", nullable: false),
                    ChildLastName = table.Column<string>(type: "varchar(50)", nullable: false),
                    ChildDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Updated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.ApplicationID);
                    table.ForeignKey(
                        name: "FK_Applications_LocalAuthorities_LocalAuthorityId",
                        column: x => x.LocalAuthorityId,
                        principalTable: "LocalAuthorities",
                        principalColumn: "LocalAuthorityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Applications_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatuses",
                columns: table => new
                {
                    ApplicationStatusID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApplicationID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatuses", x => x.ApplicationStatusID);
                    table.ForeignKey(
                        name: "FK_ApplicationStatuses_Applications_ApplicationID",
                        column: x => x.ApplicationID,
                        principalTable: "Applications",
                        principalColumn: "ApplicationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_LocalAuthorityId",
                table: "Applications",
                column: "LocalAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_SchoolId",
                table: "Applications",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatuses_ApplicationID",
                table: "ApplicationStatuses",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_LocalAuthorityId",
                table: "Schools",
                column: "LocalAuthorityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationStatuses");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Schools");

            migrationBuilder.DropTable(
                name: "LocalAuthorities");
        }
    }
}
