using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Factories.Helper;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Triangulate;

namespace CheckYourEligibility.API.Gateways.Factories
{
    public interface IStandardCheckTestScenarioFactory { }
    public class StandardCheckTestScenarioFactory : IStandardCheckTestScenarioFactory
    {
        private readonly TestDataConfiguration _testDataConfiguration;

        public StandardCheckTestScenarioFactory(TestDataConfiguration testDataConfiguration)
        {
            _testDataConfiguration = testDataConfiguration;

        }
        public bool IsTestCase(string? nino, string? nass) {

            if (!nino.IsNullOrEmpty())
            {
                return _testDataConfiguration.NinoTestScenarios.Keys.Any(prefix => nino.StartsWith(prefix));
            }

            if (!nass.IsNullOrEmpty())
            {
                var nassPrefix = nass.Substring(2, 2);
                return _testDataConfiguration.NassTestScenarios.Keys.Any(prefix => nassPrefix == prefix);
            }

            return false;
        
        }

        public (CheckEligibilityStatus, EligibilityTier?) TestDataCheck(string? nino, string? nass, CheckEligibilityType checkType)
        {

            if (!nino.IsNullOrEmpty())
            {
                if (checkType == CheckEligibilityType.FreeSchoolMeals) {

                    if (nino.StartsWith(_testDataConfiguration.EligibleTargeted)) {

                        return _testDataConfiguration.NinoTestScenarios.FirstOrDefault(x => x.Key == _testDataConfiguration.EligibleTargeted).Value;
                    }
                    if (nino.StartsWith(_testDataConfiguration.EligibleExpanded))
                    {

                        return _testDataConfiguration.NinoTestScenarios.FirstOrDefault(x => x.Key == _testDataConfiguration.EligibleExpanded).Value;
                    }
                }
                var scenario = _testDataConfiguration.NinoTestScenarios.FirstOrDefault(x => nino.StartsWith(x.Key));
                return scenario.Value;
            }
            if (!nass.IsNullOrEmpty()) 
            {
                var scenario = _testDataConfiguration.NassTestScenarios.FirstOrDefault(x => nass.StartsWith(x.Key));
                return scenario.Value;
            }
            return (CheckEligibilityStatus.parentNotFound, null);
        }
    }
}
