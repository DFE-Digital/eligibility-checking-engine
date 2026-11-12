using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSchoolToEstablishmentMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Schools_SchoolId",
                table: "Applications");


            migrationBuilder.CreateTable(
                  name: "Establishments",
                  columns: table => new
                  {
                      EstablishmentId = table.Column<int>(type: "int", nullable: false)
                          .Annotation("SqlServer:Identity", "1, 1"),
                      EstablishmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      Postcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      Locality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      Town = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      County = table.Column<string>(type: "nvarchar(max)", nullable: false),
                      StatusOpen = table.Column<bool>(type: "bit", nullable: false),
                      LocalAuthorityId = table.Column<int>(type: "int", nullable: false),
                      Type = table.Column<string>(type: "varchar(100)", nullable: true)
                  },
                  constraints: table =>
                  {
                      table.PrimaryKey("PK_Establishments", x => x.EstablishmentId);
                      table.ForeignKey(
                          name: "FK_Establishments_LocalAuthorities_LocalAuthorityId",
                          column: x => x.LocalAuthorityId,
                          principalTable: "LocalAuthorities",
                          principalColumn: "LocalAuthorityId",
                          onDelete: ReferentialAction.Cascade);
                  });

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_LocalAuthorityId",
                table: "Establishments",
                column: "LocalAuthorityId");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT Establishments ON

INSERT INTO [dbo].[Establishments]
           ([EstablishmentId]
           ,[EstablishmentName]
           ,[Postcode]
           ,[Street]
           ,[Locality]
           ,[Town]
           ,[County]
           ,[StatusOpen]
           ,[LocalAuthorityId], [Type])
     select [SchoolId]
           ,[EstablishmentName]
           ,[Postcode]
           ,[Street]
           ,[Locality]
           ,[Town]
           ,[County]
           ,[StatusOpen]
           ,[LocalAuthorityId],'Community school' from Schools

SET IDENTITY_INSERT Establishments ON
");

            migrationBuilder.RenameColumn(
                name: "SchoolId",
                table: "Applications",
                newName: "EstablishmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Applications_SchoolId",
                table: "Applications",
                newName: "IX_Applications_EstablishmentId");



            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Establishments_EstablishmentId",
                table: "Applications",
                column: "EstablishmentId",
                principalTable: "Establishments",
                principalColumn: "EstablishmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                    name: "Schools");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
