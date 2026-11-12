using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Factories.Helper;

namespace CheckYourEligibility.Core.Gateways.Factories
{
    public interface IStandardCheckTestScenarioFactory {
        (CheckEligibilityStatus, EligibilityTier?) TestDataCheck(string? nino, string? nass, CheckEligibilityType checkType);

    }
    public class StandardCheckTestScenarioFactory : IStandardCheckTestScenarioFactory
    {
        private readonly TestDataConfiguration _testDataConfiguration;

        public StandardCheckTestScenarioFactory(TestDataConfiguration testDataConfiguration)
        {
            _testDataConfiguration = testDataConfiguration;

        }
        public bool IsTestCase(string? nino, string? nass) {

            if (!string.IsNullOrEmpty(nino))
            {
                return _testDataConfiguration.NinoTestScenarios.Keys.Any(prefix => nino.StartsWith(prefix));
            }

            if (!string.IsNullOrEmpty(nass))
            {
                var nassPrefix = nass.Substring(2, 2);
                return _testDataConfiguration.NassTestScenarios.Keys.Any(prefix => nassPrefix == prefix);
            }

            return false;
        
        }

        public (CheckEligibilityStatus, EligibilityTier?) TestDataCheck(string? nino, string? nass, CheckEligibilityType checkType)
        {

            if (!string.IsNullOrEmpty(nino))
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
            if (!string.IsNullOrEmpty(nass))
               
            {
                var nassPrefix = nass.Substring(2, 2);
                var scenario = _testDataConfiguration.NassTestScenarios.FirstOrDefault(x => nassPrefix == x.Key);
                return scenario.Value;
            }
            return (CheckEligibilityStatus.parentNotFound, null);
        }
    }
}
