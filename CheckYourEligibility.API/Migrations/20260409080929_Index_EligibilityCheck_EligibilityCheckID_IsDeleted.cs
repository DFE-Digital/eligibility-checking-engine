using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Index_EligibilityCheck_EligibilityCheckID_IsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_EligibilityCheck_EligibilityCheckID_IsDeleted]
                ON [dbo].[EligibilityCheck]
                (
                    [EligibilityCheckID] ASC
                )
                INCLUDE ([IsDeleted])
                WITH (
                    STATISTICS_NORECOMPUTE = OFF,
                    DROP_EXISTING = OFF,
                    ONLINE = OFF,
                    OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX [IX_EligibilityCheck_EligibilityCheckID_IsDeleted] 
                ON [dbo].[EligibilityCheck];
            ");
        }
    }
}
