using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Index_EligibilityCheckCreatedOrgTypeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE NONCLUSTERED INDEX [ix_EligibilityCheck_Created_OrgType_Source] ON [dbo].[EligibilityCheck]
            (
	            [Created] ASC,
	            [Source] ASC,
	            [OrganisationType] ASC
            )
            INCLUDE([Type],[BulkCheckID],[OrganisationID]) WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) 
            ON [PRIMARY]");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"
            DROP INDEX [ix_EligibilityCheck_Created_OrgType_Source] ON [dbo].[EligibilityCheck];
          ");


        }
    }
}
