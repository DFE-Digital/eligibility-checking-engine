using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Factories.Helper
{
    /// <summary>
    /// Centralizes all test-related configuration in one place
    /// </summary>
    public class TestDataConfigurationWorkingFamilies {

        public string TemporaryCodeSuffix { get; set; }
        public string PermanentCodeSuffix { get; set; }
        public string FosterCodeSuffix { get; set; }
        public string ApplyDvsdNINOPrefix { get; set; }
        public string ReconfirmationStatusDueNow { get; set; }
        public string CannotBeUsedYet { get; set; }
        public string ValidForThisTerm { get; set; }
        public string ValidForThisTermAndNextTerm { get; set; }
        public string InGracePeriod { get; set; }
        public string IsExpired { get; set; }
    }
    public  class TestDataConfiguration: TestDataConfigurationWorkingFamilies
    {
        public string TestLastName { get; set; }
        public string WFTestCodePrefix { get; set; }
        public string EligiblePrefix { get; set; }
        public string EligibleTargeted { get; set; }
        public string EligibleExpanded { get; set; }
        public string InGracePeriodPrefix { get; set; }
        public string NotYetEligiblePrefix { get; set; }
        public string ExpiredPrefix { get; set; }


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
                // base test configurations
                TestLastName = configuration.GetValue<string>("TestData:LastName") ?? "TESTER",
                WFTestCodePrefix = configuration.GetValue<string>("TestData:WFTestCodePrefix") ?? "90",
                EligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Eligible") ?? "900",

                // API client side test case configurations for working families
                InGracePeriodPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:InGracePeriod") ?? "901",
                NotYetEligiblePrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:NotYetEligible") ?? "902",
                ExpiredPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode:Expired") ?? "903",
                EligibleTargeted = configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleTargeted") ?? "NA",
                EligibleExpanded = configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleExpanded") ?? "NE",

                //API internal side test case confifurations for working families
                CannotBeUsedYet = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Prefixes:cannotBeUsedYet") ?? "700",
                ValidForThisTerm = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Prefixes:validForThisTerm") ?? "701",
                ValidForThisTermAndNextTerm  = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Prefixes:validForThisTermAndNextTerm") ?? "702",
                InGracePeriod = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Prefixes:inGracePeriod") ?? "703",
                IsExpired  = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Prefixes:isExpired") ?? "704",

                //API internal side test case scenario configurations for working families
                TemporaryCodeSuffix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Scenarios:temporaryCodeSuffix") ?? "4",
                PermanentCodeSuffix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Scenarios:permanentCodeSuffix") ?? "9",
                FosterCodeSuffix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Scenarios:fosterCodeSuffix") ?? "1",
                ApplyDvsdNINOPrefix = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Scenarios:applyDvsdNINOPrefix") ?? "NN",
                ReconfirmationStatusDueNow = configuration.GetValue<string>("TestData:Outcomes:EligibilityCode-Frontend:Scenarios:ReconfirmationStatusDueNow") ?? "C",

            }; 

            // Setup NINO scenarios
            config.NinoTestScenarios = new Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)>{
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Eligible") ?? "NN",
                (CheckEligibilityStatus.eligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:NotEligible") ?? "PN",
                (CheckEligibilityStatus.notEligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:ParentNotFound") ?? "RA",
                (CheckEligibilityStatus.parentNotFound, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:Error") ?? "XX",
                (CheckEligibilityStatus.error, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleTargeted") ?? "NA",
                (CheckEligibilityStatus.eligible, EligibilityTier.targeted)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalInsuranceNumber:EligibleExpanded") ?? "NE",
                (CheckEligibilityStatus.eligible, EligibilityTier.expanded)
            }
        };

            // Setup NASS scenarios
            config.NassTestScenarios = new Dictionary<string, (CheckEligibilityStatus, EligibilityTier?)>
        {
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Eligible") ?? "01",
                (CheckEligibilityStatus.eligible, EligibilityTier.targeted)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:NotEligible") ?? "02",
                (CheckEligibilityStatus.notEligible, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:ParentNotFound") ?? "08",
                (CheckEligibilityStatus.parentNotFound, null)
            },
            {
                configuration.GetValue<string>("TestData:Outcomes:NationalAsylumSeekerServiceNumber:Error") ?? "07",
                (CheckEligibilityStatus.error, null)
            }
        };

            return config;
        }
    }
}
