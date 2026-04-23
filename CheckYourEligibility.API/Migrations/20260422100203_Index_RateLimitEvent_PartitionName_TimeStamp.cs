using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Index_RateLimitEvent_PartitionName_TimeStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_RateLimitEvent_PartitionName_TimeStamp",
                table: "RateLimitEvents",
                columns: new[] { "PartitionName", "TimeStamp" })
                .Annotation("SqlServer:Include", new[] { "QuerySize" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_RateLimitEvent_PartitionName_TimeStamp",
                table: "RateLimitEvents");
        }
    }
}
