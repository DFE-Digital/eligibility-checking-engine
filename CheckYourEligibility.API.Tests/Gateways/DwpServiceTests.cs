// Ignore Spelling: Levenshtein

using System.Net;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Resources = CheckYourEligibility.API.Tests.Properties.Resources;

namespace CheckYourEligibility.API.Tests;

public class DwpServiceTests : TestBase.TestBase
{
    // private IEligibilityCheckContext _fakeInMemoryDb;
    private IConfiguration _configuration;
    private DwpGateway _sut;
    private HttpClient httpClient;

    [SetUp]
    public void Setup()
    {
        httpClient = new HttpClient();
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        //"c": "ecs.education.gov.uk",
        //"EcsServiceVersion": "20170701",
        //"EcsLAId": "999",
        //"EcsSystemId": "ECE43342",
        //"EcsPassword": "jiK65zxTmJ",


        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var configForSmsApi = new Dictionary<string, string>
        {
            { "Dwp:UniversalCreditThreshhold-1", "616.66" },
            { "Dwp:UniversalCreditThreshhold-2", "1233.33" },
            { "Dwp:UniversalCreditThreshhold-3", "1849.99" },
            { "Dwp:EcsHost", "ecs.education.gov.uk" },
            { "Dwp:EcsServiceVersion", "20170701" },
            { "Dwp:EcsLAId", "999" },
            { "Dwp:EcsSystemId", "testId" },
            { "Dwp:EcsPassword", "testpassword" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";


        _sut = new DwpGateway(new NullLoggerFactory(), httpClient, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
    }


    [Test]
    public async Task Given_Valid_EcsFsmCheck_Should_Return_SoapFsmCheckRespone()
    {
        // Arrange
        var request = _fixture.Create<CheckProcessData>();

        var handlerMock = new Mock<HttpMessageHandler>();
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(Resources.EcsSoapEligible)
        };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
        var httpClient = new HttpClient(handlerMock.Object);
        _sut = new DwpGateway(new NullLoggerFactory(), httpClient, _configuration);

        // Act
        var response = await _sut.EcsFsmCheck(request);

        // Assert
        response.Should().BeOfType<SoapFsmCheckRespone>();
    }

    [Test]
    public async Task Given_InvalidValid_EcsFsmCheck_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<EligibilityCheck>();

        var handlerMock = new Mock<HttpMessageHandler>();
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent(Resources.EcsSoapEligible)
        };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
        var httpClient = new HttpClient(handlerMock.Object);
        _sut = new DwpGateway(new NullLoggerFactory(), httpClient, _configuration);

        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        var dataItem = GetCheckProcessData(fsm);


        // Act
        var response = await _sut.EcsFsmCheck(dataItem);

        // Assert
        response.Should().BeNull();
    }


    [Test]
    public void Given_Claims_have_pensions_credit_CheckBenefitEntitlement_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.status = DwpGateway.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    [Test]
    public void Given_Claims_have_pensions_credit_CheckBenefitEntitlement_Should_Return_false()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.status = "not entitled";
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(false);
    }

    [Test]
    public void Given_Claims_have_job_seekers_allowance_income_based_CheckBenefitEntitlement_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.job_seekers_allowance_income_based.ToString();
        request.data[0].attributes.status = DwpGateway.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    [Test]
    public void Given_Claims_have_income_support_CheckBenefitEntitlement_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.income_support.ToString();
        request.data[0].attributes.status = DwpGateway.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    [Test]
    public void Given_Claims_have_employment_support_allowance_income_based_CheckBenefitEntitlement_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.employment_support_allowance_income_based.ToString();
        request.data[0].attributes.status = DwpGateway.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    /// <summary>
    ///     UC1 616.66 One instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_1_Should_Return_false()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 10000 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(false);
    }

    /// <summary>
    ///     UC1 616.66 One instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_1_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-04-29", startDate = "2022-03-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }


    /// <summary>
    ///     UC2 1233.33 two instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_2_Should_Return_false()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-04-29", startDate = "2022-03-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 5000 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 5000 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(false);
    }

    /// <summary>
    ///     UC1 1233.33 two instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_2_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-04-29", startDate = "2022-03-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    /// <summary>
    ///     UC2 1849.99 two instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_3_Should_Return_false()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-04-29", startDate = "2022-03-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 5000 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 5000 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 5000 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(false);
    }

    /// <summary>
    ///     UC1 1849.99 two instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_3_Should_Return_true()
    {
        // Arrange
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpGateway.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = "2022-04-29", startDate = "2022-03-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            },
            new()
            {
                endDate = "2022-05-29", startDate = "2022-04-30",
                status = DwpGateway.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request);

        // Assert
        response.Should().Be(true);
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth,
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = new CheckEligibilityRequestData().CheckType
        };
    }
}