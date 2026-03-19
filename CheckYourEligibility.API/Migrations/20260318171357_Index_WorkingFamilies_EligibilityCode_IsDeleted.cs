using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    public partial class Index_WorkingFamilies_EligibilityCode_IsDeleted : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_WorkingFamiliesEvents_EligibilityCode_IsDeleted] 
                ON [dbo].[WorkingFamiliesEvents]
                (
                    [EligibilityCode] ASC,
                    [IsDeleted] ASC
                )
                WITH (
                    STATISTICS_NORECOMPUTE = OFF, 
                    DROP_EXISTING = OFF, 
                    ONLINE = OFF, 
                    OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX [IX_WorkingFamiliesEvents_EligibilityCode_IsDeleted] 
                ON [dbo].[WorkingFamiliesEvents];
            ");
        }
    }
}