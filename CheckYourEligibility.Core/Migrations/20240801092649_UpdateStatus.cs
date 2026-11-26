using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.Applications SET Status = 'Entitled' WHERE status = 'Open'");
            migrationBuilder.Sql("UPDATE dbo.Applications SET Status = 'SentForReview' WHERE status = 'PendingApproval'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
