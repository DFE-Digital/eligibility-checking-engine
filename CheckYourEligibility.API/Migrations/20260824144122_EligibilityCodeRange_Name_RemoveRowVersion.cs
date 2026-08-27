using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class EligibilityCodeRange_Name_RemoveRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EligibilityCodeRanges");

            // Add Name as nullable temporarily so the existing range can be labelled.
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "EligibilityCodeRanges",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // This is the existing Foster range created by the preceding migration.
            migrationBuilder.Sql(
                """
                UPDATE [EligibilityCodeRanges]
                SET [Name] = N'Foster'
                WHERE [EligibilityCodeRangeId] = 1;
                """);

            // Every range must have a name after the existing row has been updated.
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "EligibilityCodeRanges",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityCodeRanges_Name",
                table: "EligibilityCodeRanges",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EligibilityCodeRanges_Name",
                table: "EligibilityCodeRanges");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "EligibilityCodeRanges");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EligibilityCodeRanges",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }
    }
}