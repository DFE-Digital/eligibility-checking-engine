using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_TemporaryCodeUpload_ImportedBy_ForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImportedBy",
                table: "TemporaryCodeUploads",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryCodeUploads_ImportedBy",
                table: "TemporaryCodeUploads",
                column: "ImportedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_TemporaryCodeUploads_Users_ImportedBy",
                table: "TemporaryCodeUploads",
                column: "ImportedBy",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TemporaryCodeUploads_Users_ImportedBy",
                table: "TemporaryCodeUploads");

            migrationBuilder.DropIndex(
                name: "IX_TemporaryCodeUploads_ImportedBy",
                table: "TemporaryCodeUploads");

            migrationBuilder.AlterColumn<Guid>(
                name: "ImportedBy",
                table: "TemporaryCodeUploads",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
