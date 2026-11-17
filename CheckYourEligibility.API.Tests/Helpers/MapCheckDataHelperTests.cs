using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
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

    [Test]
    public void BuildCheckProcessedData_WorkingFamilies_ReturnsLastNameFromCheckData()
    {
        var checkData = new CheckProcessData { LastName = "CheckSurname" };
        var wfEvent = new WorkingFamiliesEvent { ParentLastName = "EventSurname" };

        var result = MapCheckDataHelper.BuildCheckProcessedData_WorkingFamilies(
            wfEvent,
            isGracePeriodEndDateApplied: false,
            checkData);

        result.LastName.Should().Be("CheckSurname");
    }

    [Test]
    public void BuildCheckProcessedData_WorkingFamilies_DoesNotReturnLastNameIfNotInRequest()
    {
        var checkData = new CheckProcessData();
        var wfEvent = new WorkingFamiliesEvent { ParentLastName = "EventSurname" };

        var result = MapCheckDataHelper.BuildCheckProcessedData_WorkingFamilies(
            wfEvent,
            isGracePeriodEndDateApplied: false,
            checkData);

        result.LastName.Should().BeNull();
    }
}