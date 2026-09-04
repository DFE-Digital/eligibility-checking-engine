using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests.Services;

[TestFixture]
public class EligibilityCheckDataResponseMapperTests
{
    private EligibilityCheckDataResponseMapper _sut;

    [SetUp]
    public void Setup()
    {
        _sut = new EligibilityCheckDataResponseMapper(
            Mock.Of<ILogger<EligibilityCheckDataResponseMapper>>(),
            Mock.Of<ICheckEligibility>());
    }

    [Test]
    public void MapCheckDataToResponse_WorkingFamilies_Returns_WorkingFamiliesItem()
    {
        var request = new CheckEligibilityRequestWorkingFamiliesBulkData
        {
            EligibilityCode = "ABC123",
            LastName = "smith"
        };

        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.WorkingFamilies,
            CheckData = JsonConvert.SerializeObject(request)
        };

        var result = _sut.MapCheckDataToResponse(check);

        result.Should().BeOfType<CheckEligibilityWorkingFamiliesItem>();
    }

    [Test]
    public void MapCheckDataToResponse_FreeSchoolMeals_Returns_StandardItem()
    {
        var request = new CheckEligibilityRequestBulkData
        {
            LastName = "Smith"
        };

        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.FreeSchoolMeals,
            CheckData = JsonConvert.SerializeObject(request)
        };

        var result = _sut.MapCheckDataToResponse(check);

        result.Should().BeOfType<CheckEligibilityItem>();
    }

    [Test]
    public void MapCheckDataToResponseStandard_Maps_All_Fields()
    {
        var request = new CheckProcessData
        {
            FirstName = "John",
            LastName = "SMITH",
            ChildFirstName = "Child",
            ChildLastName = "SMITH",
            NationalInsuranceNumber = "AB123456C",
            DateOfBirth = "1980-01-01",
            ClientIdentifier = "CLIENT1",
            Order = 7,
            EligibilityEndDate = "2025-12-31",
            EmailAddress = "test@test.com"
        };

        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.FreeSchoolMeals,
            Status = CheckEligibilityStatus.eligible,
            Created = DateTime.UtcNow,
            CheckData = JsonConvert.SerializeObject(request)
        };

        CheckEligibilityItem result = (CheckEligibilityItem)_sut.MapCheckDataToResponse(check);

        result.Status.Should().Be("eligible");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("SMITH");
        result.ClientIdentifier.Should().Be("CLIENT1");
        result.Order.Should().Be(7);
        result.EmailAddress.Should().Be("test@test.com");
    }

    [TestCase("2024-12-31", null)]
    [TestCase("2025-01-01", "2024-12-31")]
    public void MapCheckDataToResponseWorkingFamilies_ExternalMapping_MapsValidityStartDate(string vsd, string dvsd)
    {
        var request = new CheckProcessData
        {
            EligibilityCode = "CODE123",
            LastName = "SMITH",
            NationalInsuranceNumber = "AB123456C",
            ValidityStartDate = vsd,
            DiscretionaryValidityStartDate = dvsd,
            ValidityEndDate = "2025-12-31",
            GracePeriodEndDate = "2026-03-31",
            DateOfBirth = "1980-01-01"
        };

        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.WorkingFamilies,
            Status = CheckEligibilityStatus.eligible,
            CheckData = JsonConvert.SerializeObject(request)
        };

        var result = _sut.MapCheckDataToResponseWorkingFamilies(check);

        result.Status.Should().Be("eligible");
        result.EligibilityCode.Should().Be("CODE123");
        result.LastName.Should().Be("SMITH");
        // for external check if DVSD is found in checkData map it to VSD , else use old VSD value for old checks
        result.ValidityStartDate.Should().Be(dvsd != null ? dvsd:vsd);
        result.ValidityEndDate.Should().Be("2025-12-31");
        result.GracePeriodEndDate.Should().Be("2026-03-31");
    }

    [Test]
    public void MapCheckDataToResponseWorkingFamilies_InternalMapping_ReturnsBothStartDates()
    {
        var request = new CheckProcessData
        {
            EligibilityCode = "CODE123",
            LastName = "SMITH",
            NationalInsuranceNumber = "AB123456C",
            ValidityStartDate = "2025-01-01",
            DiscretionaryValidityStartDate = "2024-12-31",
            ValidityEndDate = "2025-12-31",
            GracePeriodEndDate = "2026-03-31",
            DateOfBirth = "1980-01-01",
            Order = 8
        };

        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.WorkingFamilies,
            Status = CheckEligibilityStatus.eligible,
            CheckData = JsonConvert.SerializeObject(request)
        };

        var result = _sut.MapCheckDataToResponseWorkingFamilies(check, isInternal: true);
        result.EligibilityCode.Should().Be("CODE123");
        result.LastName.Should().Be("SMITH");
        result.NationalInsuranceNumber.Should().Be("AB123456C");
        result.ValidityStartDate.Should().Be("2025-01-01");
        result.DiscretionaryValidityStartDate.Should().Be("2024-12-31");
        result.GracePeriodEndDate.Should().Be("2026-03-31");
        result.DateOfBirth.Should().Be("1980-01-01");
        result.Order.Should().Be(8);
    }

    [Test]
    public void MapCheckDataToResponseStandard_Sets_EligibilityCheckId_When_BulkCheckId_Exists()
    {
        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.FreeSchoolMeals,
            BulkCheckID = Guid.NewGuid().ToString(),
            EligibilityCheckID = Guid.NewGuid().ToString(),
            CheckData = JsonConvert.SerializeObject(
                new CheckEligibilityRequestBulkData())
        };

        var result = _sut.MapCheckDataToResponse(check);

        result.EligibilityCheckID.Should().Be(check.EligibilityCheckID);
    }

    [Test]
    public void MapCheckDataToResponseStandard_Does_Not_Set_EligibilityCheckId_When_BulkCheckId_Is_Null()
    {
        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.FreeSchoolMeals,
            BulkCheckID = null,
            EligibilityCheckID = Guid.NewGuid().ToString(),
            CheckData = JsonConvert.SerializeObject(
                new CheckEligibilityRequestBulkData())
        };

        var result = _sut.MapCheckDataToResponse(check);

        result.EligibilityCheckID.Should().BeNull();
    }

    [Test]
    public void MapCheckDataToResponseStandard_With_Null_CheckData_Returns_Empty_Item()
    {
        var check = new EligibilityCheck
        {
            Type = CheckEligibilityType.FreeSchoolMeals,
            CheckData = null
        };

        var result = _sut.MapCheckDataToResponse(check);

        result.Should().NotBeNull();
    }
}