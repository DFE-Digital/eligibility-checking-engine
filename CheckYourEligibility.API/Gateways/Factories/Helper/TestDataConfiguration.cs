using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Factories.Helper
{
    /// <summary>
    /// Centralizes all test-related configuration in one place
    /// </summary>
    public  class TestDataConfiguration
    {
        public string? TestLastName { get; set; }
        public string? WFTestCodePrefix { get; set; }
        public string? EligiblePrefix { get; set; }
        public string? InGracePeriodPrefix { get; set; }
        public string? NotYetEligiblePrefix { get; set; }
        public string? ExpiredPrefix { get; set; }

        // NINO test prefixes
        public Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)> NinoTestScenarios { get; set; } = new();

        // NASS test prefixes
        public Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)> NassTestScenarios { get; set; } = new();

        /// <summary>
        /// Helper method to create configuration from IConfiguration
        /// </summary>
        public static TestDataConfiguration CreateFromConfiguration(IConfiguration configuration)
        {
            var config = new TestDataConfiguration
            {
                TestLastName = configuration.GetValue<string>("TestData:LastName"),
                WFTestCodePrefix = configuration.GetValue<string>("TestData:WFTestCodePrefix"),
                EligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible"),
                InGracePeriodPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:InGracePeriod"),
                NotYetEligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:NotYetEligible"),
                ExpiredPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Expired")
            };

            // Setup NINO scenarios
            config.NinoTestScenarios = new Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)>
        {
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Eligible") ?? "",
                (CheckEligibilityStatus.eligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:NotEligible") ?? "",
                (CheckEligibilityStatus.notEligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:ParentNotFound") ?? "",
                (CheckEligibilityStatus.parentNotFound, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Error") ?? "",
                (CheckEligibilityStatus.error, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleTargeted") ?? "",
                (CheckEligibilityStatus.eligible, EligibilityTier.targeted)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleExpanded") ?? "",
                (CheckEligibilityStatus.eligible, EligibilityTier.expanded)
            }
        };

            // Setup NASS scenarios
            config.NassTestScenarios = new Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)>
        {
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Eligible") ?? "",
                (CheckEligibilityStatus.eligible, EligibilityTier.targeted)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:NotEligible") ?? "",
                (CheckEligibilityStatus.notEligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:ParentNotFound") ?? "",
                (CheckEligibilityStatus.parentNotFound, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Error") ?? "",
                (CheckEligibilityStatus.error, null)
            }
        };

            return config;
        }
    }
}
