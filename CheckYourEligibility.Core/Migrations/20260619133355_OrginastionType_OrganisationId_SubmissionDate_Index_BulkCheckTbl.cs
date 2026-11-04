using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.Core.Migrations
{
    /// <inheritdoc />
    public partial class OrginastionType_OrganisationId_SubmissionDate_Index_BulkCheckTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"
            CREATE NONCLUSTERED INDEX [IX_BulkChecks_OrgType_OrgID_SubmittedDate]
            ON [dbo].[BulkChecks]
            (
                [OrganisationType] ASC,
                [OrganisationID] ASC,
                [SubmittedDate] DESC
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
                DROP INDEX [IX_BulkChecks_OrgType_OrgID_SubmittedDate]
                ON [dbo].[BulkChecks];
            ");
        }
    }
}
