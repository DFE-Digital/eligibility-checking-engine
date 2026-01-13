using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_FosterCarer_LocalAuthority_FK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LocalAuthorityId",
                table: "FosterCarers",
                newName: "LocalAuthorityID");

            migrationBuilder.AlterColumn<int>(
                name: "LocalAuthorityID",
                table: "FosterCarers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_FosterCarers_LocalAuthorityID",
                table: "FosterCarers",
                column: "LocalAuthorityID");

            migrationBuilder.AddForeignKey(
                name: "FK_FosterCarers_LocalAuthorities_LocalAuthorityID",
                table: "FosterCarers",
                column: "LocalAuthorityID",
                principalTable: "LocalAuthorities",
                principalColumn: "LocalAuthorityID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FosterCarers_LocalAuthorities_LocalAuthorityID",
                table: "FosterCarers");

            migrationBuilder.DropIndex(
                name: "IX_FosterCarers_LocalAuthorityID",
                table: "FosterCarers");

            migrationBuilder.RenameColumn(
                name: "LocalAuthorityID",
                table: "FosterCarers",
                newName: "LocalAuthorityId");

            migrationBuilder.AlterColumn<int>(
                name: "LocalAuthorityId",
                table: "FosterCarers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
