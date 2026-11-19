using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Database;

[ExcludeFromCodeCoverage]
public static class DbInitializer
{
    public static void Initialize(EligibilityCheckContext context)
    {
        context.Database.EnsureCreated();

        // Look for any FreeSchoolMealsHMRC.
        if (!context.FreeSchoolMealsHMRC.Any())
        {
            var fsmHmrc = new[]
            {
                new FreeSchoolMealsHMRC
                {
                    FreeSchoolMealsHMRCID = "AB123456C", Surname = "SIMPSON",
                    DateOfBirth = DateTime.Parse("1990-01-01"), DataType = 1
                },
                new FreeSchoolMealsHMRC
                {
                    FreeSchoolMealsHMRCID = "AC123456D", Surname = "GRIFFIN",
                    DateOfBirth = DateTime.Parse("2000-12-31"), DataType = 1
                }
            };
            foreach (var s in fsmHmrc) context.FreeSchoolMealsHMRC.Add(s);
        }

        // Look for any FreeSchoolMealsHO.
        if (!context.FreeSchoolMealsHO.Any())
        {
            var fsmHo = new[]
            {
                new FreeSchoolMealsHO
                {
                    FreeSchoolMealsHOID = Guid.NewGuid().ToString(), FirstName = "HOMER", LastName = "SIMPSON",
                    DateOfBirth = DateTime.Parse("1990-01-01"), NASS = "240712349",
                    PortReferenceNumber = "MST/9011357"
                },
                new FreeSchoolMealsHO
                {
                    FreeSchoolMealsHOID = Guid.NewGuid().ToString(), FirstName = "PETER", LastName = "GRIFFIN",
                    DateOfBirth = DateTime.Parse("2000-12-31"), NASS = "240712350",
                    PortReferenceNumber = "TN4/8689288"
                }
            };
            foreach (var s in fsmHo) context.FreeSchoolMealsHO.Add(s);

            context.SaveChanges();
        }
    }
}