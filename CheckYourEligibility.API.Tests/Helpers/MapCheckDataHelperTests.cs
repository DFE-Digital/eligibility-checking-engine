using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Helpers;
using FluentAssertions;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests.Helpers;

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