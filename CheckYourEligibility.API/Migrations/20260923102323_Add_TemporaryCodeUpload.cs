using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_TemporaryCodeUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemporaryCodeUploads",
                columns: table => new
                {
                    TemporaryCodeUploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Filename = table.Column<string>(type: "varchar(200)", nullable: false),
                    NumberOfCodes = table.Column<int>(type: "int", nullable: false),
                    FirstCode = table.Column<string>(type: "varchar(12)", nullable: false),
                    LastCode = table.Column<string>(type: "varchar(12)", nullable: false),
                    DateImported = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryCodeUploads", x => x.TemporaryCodeUploadId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemporaryCodeUploads");
        }
    }
}
