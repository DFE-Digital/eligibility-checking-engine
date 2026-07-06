using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckYourEligibility.API.Migrations
{
    /// <inheritdoc />
    public partial class Update_Existing_User_Records_UserType_FreeSchoolMealsParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"
            UPDATE Users
            SET UserType = 'FreeSchoolMealsParent'
            WHERE UserType IS NULL
        ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            UPDATE Users
            SET UserType = NULL
            WHERE UserType = 'FreeSchoolMealsParent'
            ");
        }
    }
}
