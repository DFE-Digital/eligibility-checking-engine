using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Index_EligbilityCheck_OrgType_Created_Type_Status : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
             CREATE NONCLUSTERED INDEX [IX_OrganisationType_Created_Type_Status] ON [dbo].[EligibilityCheck]
            (
	            [OrganisationType] ASC,
	            [Created] ASC
            )
            INCLUDE([Type],[Status],[OrganisationID]) WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
            GO");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"
            DROP INDEX [IX_OrganisationType_Created_Type_Status] ON [dbo].[EligibilityCheck];
          ");


        }
    }
}
