using System.Net;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Resources = CheckYourEligibility.API.Tests.Properties.Resources;

namespace CheckYourEligibility.API.Tests;

public class EcsAdapterTests : TestBase
{
    // private IEligibilityCheckContext _fakeInMemoryDb;
    private IConfiguration _configuration;
    private EcsAdapter _sut;
    private HttpClient httpClient;

    [SetUp]
    public void Setup()
    {
        httpClient = new HttpClient();   

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var configForSmsApi = new Dictionary<string, string>
        {
            { "Dwp:UniversalCreditThreshhold-1", "616.66" },
            { "Dwp:UniversalCreditThreshhold-2", "1233.33" },
            { "Dwp:UniversalCreditThreshhold-3", "1849.99" },
            { "Dwp:ApiUniversalCreditThreshold:FreeSchoolMeals", "616.66" },
            { "Dwp:EcsHost", "ecs.education.gov.uk" },
            { "Dwp:EcsServiceVersion", "20170701" },
            { "Dwp:EcsLAId", "999" },
            { "Dwp:EcsSystemId", "testId" },
            { "Dwp:EcsPassword", "testpassword" },
            { "Dwp:UseEcsForChecks", "true" },
            { "Dwp:UseEcsForChecksWF", "true" },
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";


        _sut = new EcsAdapter(new NullLoggerFactory(), httpClient, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public async Task Given_Valid_EcsFsmCheck_Should_Return_SoapCheckResponse()
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
        _sut = new EcsAdapter(new NullLoggerFactory(), httpClient, _configuration);

        // Act
        var response = await _sut.EcsCheck(request, CheckEligibilityType.FreeSchoolMeals, _configuration["Dwp:EcsLAId"]);

        // Assert
        response.Should().BeOfType<SoapCheckResponse>();
    }

    [Test]
    public async Task Given_InvalidValid_EcsFsmCheck_Should_Return_null()
    {
        // Arrange
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
        _sut = new EcsAdapter(new NullLoggerFactory(), httpClient, _configuration);

        var fsm = _fixture.Create<CheckEligibilityRequestData>();
        fsm.DateOfBirth = "1990-01-01";
        fsm.Type = CheckEligibilityType.FreeSchoolMeals;
        var dataItem = GetCheckProcessData(fsm);


        // Act
        var response = await _sut.EcsCheck(dataItem, CheckEligibilityType.TwoYearOffer, _configuration["Dwp:EcsLAId"]);

        // Assert
        response.Should().BeNull();
    }

    [Test]
    public async Task Given_Valid_EcsWFCheck_Should_Return_SoapWFCheckRespone()
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
        _sut = new EcsAdapter(new NullLoggerFactory(), httpClient, _configuration);

        // Act
        var response = await _sut.EcsWFCheck(request, _configuration["Dwp:EcsLAId"]);

        // Assert
        response.Should().BeOfType<SoapCheckResponse>();
    }

    [Test]
    public async Task Given_Invalid_EcsWFCheck_Should_Return_null()
    {
        // Arrange
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
        _sut = new EcsAdapter(new NullLoggerFactory(), httpClient, _configuration);

        var wf = _fixture.Create<CheckEligibilityRequestData>();
        wf.DateOfBirth = "1990-01-01";
        wf.Type = CheckEligibilityType.WorkingFamilies;
        var dataItem = GetCheckProcessData(wf);


        // Act
        var response = await _sut.EcsWFCheck(dataItem, "999");

        // Assert
        response.Should().BeNull();
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth ?? "1990-01-01",
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = request.Type
        };
    }
}