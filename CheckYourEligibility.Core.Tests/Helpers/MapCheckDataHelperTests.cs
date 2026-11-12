using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Helpers;
using FluentAssertions;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Tests.Helpers;

[TestFixture]
public class MapCheckDataHelperTests
{
    [TestCase(CheckEligibilityType.FreeSchoolMeals)]
    [TestCase(CheckEligibilityType.TwoYearOffer)]
    [TestCase(CheckEligibilityType.EarlyYearPupilPremium)]
    public void MapCheckDataBasedOnType_StandardBulkCheck_PreservesOrder(
        CheckEligibilityType type)
    {
        var request = new CheckEligibilityRequestBulkData
        {
            Order = 7
        };

        var result = MapCheckDataHelper.MapCheckDataBasedOnType(
            type,
            JsonConvert.SerializeObject(request));

        result.Order.Should().Be(7);
    }

    [Test]
    public void MapCheckDataBasedOnType_WorkingFamiliesBulkCheck_PreservesOrder()
    {
        var request = new CheckEligibilityRequestWorkingFamiliesBulkData
        {
            Order = 8
        };

        var result = MapCheckDataHelper.MapCheckDataBasedOnType(
            CheckEligibilityType.WorkingFamilies,
            JsonConvert.SerializeObject(request));

        result.Order.Should().Be(8);
    }
}