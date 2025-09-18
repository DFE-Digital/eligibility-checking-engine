using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeBulkCheckQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create composite index for optimal bulk check queries
            // This index covers the most common query pattern: filtering by SubmittedDate and LocalAuthorityId
            migrationBuilder.CreateIndex(
                name: "IX_BulkChecks_SubmittedDate_LocalAuthorityId",
                table: "BulkChecks",
                columns: new[] { "SubmittedDate", "LocalAuthorityId" });

            // Create index on SubmittedDate alone for admin users (who don't filter by LocalAuthorityId)
            migrationBuilder.CreateIndex(
                name: "IX_BulkChecks_SubmittedDate",
                table: "BulkChecks",
                column: "SubmittedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the performance optimization indexes
            migrationBuilder.DropIndex(
                name: "IX_BulkChecks_SubmittedDate_LocalAuthorityId",
                table: "BulkChecks");

            migrationBuilder.DropIndex(
                name: "IX_BulkChecks_SubmittedDate",
                table: "BulkChecks");
        }
    }
}
