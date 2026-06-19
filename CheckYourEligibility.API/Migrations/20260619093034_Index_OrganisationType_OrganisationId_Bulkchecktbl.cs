using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Index_OrganisationType_OrganisationId_Bulkchecktbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_BulkChecks_OrganisationType_OrganisationID]
                ON [dbo].[BulkChecks]
                (
                    [OrganisationType] ASC,
                    [OrganisationID] ASC
                )
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
                DROP INDEX [IX_BulkChecks_OrganisationType_OrganisationID]
                ON [dbo].[BulkChecks];
            ");

        }
    }
}
