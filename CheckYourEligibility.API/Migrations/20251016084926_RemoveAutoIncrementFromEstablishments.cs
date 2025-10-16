using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAutoIncrementFromEstablishments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewEstablishmentId",
                table: "Establishments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Establishments SET NewEstablishmentId = EstablishmentId");

            migrationBuilder.Sql(
                "ALTER TABLE Applications DROP CONSTRAINT FK_Applications_Establishments_EstablishmentId");
            
            migrationBuilder.Sql(
                "ALTER TABLE Establishments DROP CONSTRAINT PK_Establishments");

            migrationBuilder.DropColumn(
                "EstablishmentId",
                "Establishments");
            
            
            migrationBuilder.RenameColumn(
                "NewEstablishmentId",
                "Establishments",
                "EstablishmentId");
            
            migrationBuilder.Sql("ALTER TABLE Establishments ADD CONSTRAINT PK_Establishments PRIMARY KEY (EstablishmentId)");
            migrationBuilder.Sql("ALTER TABLE Applications ADD CONSTRAINT FK_Applications_Establishments_EstablishmentId FOREIGN KEY (EstablishmentId) REFERENCES Establishments (EstablishmentId) ON DELETE CASCADE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
