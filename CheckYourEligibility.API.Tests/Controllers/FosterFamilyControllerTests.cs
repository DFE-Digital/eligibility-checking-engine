using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ValidationException = FluentValidation.ValidationException;

namespace CheckYourEligibility.API.Tests;

public class FosterFamilyControllerTests : TestBase.TestBase
{
    private IConfigurationRoot _configuration = null!;
    //private new Fixture _fixture = null!; // Added 'new' keyword
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ICreateFosterFamilyUseCase> _mockCreateFosterFamilyUseCase = null!;
    private Mock<IGetFosterFamilyUseCase> _mockGetFosterFamilyUseCase = null!;
    private Mock<IUpdateFosterFamilyUseCase> _mockUpdateFosterFamilyUseCase = null!;
    private ILogger<FosterFamilyController> _mockLogger = null!;
    private FosterFamilyController _sut = null!;
    private FosterFamilyRequest fosterFamilyRequest = null!;
    private FosterFamilyUpdateRequest fosterFamilyUpdateRequest = null!;
    private string myGuid = null!;

    [SetUp]
    public void Setup()
    {
        _mockCreateFosterFamilyUseCase = new Mock<ICreateFosterFamilyUseCase>(MockBehavior.Strict);
        _mockGetFosterFamilyUseCase = new Mock<IGetFosterFamilyUseCase>(MockBehavior.Strict);
        _mockUpdateFosterFamilyUseCase = new Mock<IUpdateFosterFamilyUseCase>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<FosterFamilyController>>();
        //_fixture = new Fixture(); // Ensure _fixture is initialized
        fosterFamilyRequest = new FosterFamilyRequest
        {
            Data = new FosterFamilyRequestData
            {
                CarerFirstName = "John",
                CarerLastName = "Doe",
                CarerDateOfBirth = new DateTime(1980, 5, 15),
                CarerNationalInsuranceNumber = "AB123456C",
                HasPartner = false,
                PartnerFirstName = null,
                PartnerLastName = null,
                PartnerDateOfBirth = null,
                PartnerNationalInsuranceNumber = null,
                ChildFirstName = "Emily",
                ChildLastName = "Doe",
                ChildDateOfBirth = new DateTime(2015, 3, 10),
                ChildPostCode = "SW1A 1AA",
                SubmissionDate = DateTime.UtcNow
            }
        };

        myGuid = "ee0e0128-9887-4482-8df4-7922b8c8f53a";
        fosterFamilyUpdateRequest =
                new FosterFamilyUpdateRequest
                {
                    CarerFirstName = "Sarah",
                    CarerLastName = "Thompson",
                    CarerDateOfBirth = new DateTime(1985, 4, 12),
                    CarerNationalInsuranceNumber = "AB123456C",

                    HasPartner = true,

                    PartnerFirstName = "David",
                    PartnerLastName = "Thompson",
                    PartnerDateOfBirth = new DateTime(1983, 11, 2),
                    PartnerNationalInsuranceNumber = "ZX987654A",

                    ChildFirstName = "Emily",
                    ChildLastName = "Thompson",
                    ChildDateOfBirth = new DateTime(2015, 6, 21),
                    ChildPostCode = "LS12 4AB"
                };


        // config data for Jwt:Scopes:local_authority
        var configData = new Dictionary<string, string?> // Changed to string? for value
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData) // Now matches expected type
            .Build();

        _sut = new FosterFamilyController(
            _mockLogger,
            _mockCreateFosterFamilyUseCase.Object,
            _mockGetFosterFamilyUseCase.Object,
            _mockUpdateFosterFamilyUseCase.Object,
            _mockAuditGateway.Object,
            _configuration
            );
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateFosterFamilyUseCase.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    #region Post Foster Family

    [Test]
    public async Task FosterFamily_ShouldReturnCreatedResponse201_WhenUseCaseSucceeds()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        _mockCreateFosterFamilyUseCase
            .Setup(uc => uc.Execute(fosterFamilyRequest, localAuthorityIds))
            .ReturnsAsync(new FosterFamilySaveItemResponse());

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.FosterFamily(fosterFamilyRequest);

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)actionResult;

        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().BeEquivalentTo(new FosterFamilySaveItemResponse());
    }

    [Test]
    public async Task FosterFamily_ShouldReturnBadRequest_WhenNoLocalAuthorityScopeFound()
    {
        // Arrange

        // Setup controller without local authority scope
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var actionResult = await _sut.FosterFamily(fosterFamilyRequest);

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;

        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = (ErrorResponse)badRequestResult.Value;

        errorResponse.Errors.Should().ContainSingle()
            .Which.Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task FosterFamily_ShouldReturnBadRequest_WhenValidationExceptionThrown()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        _mockCreateFosterFamilyUseCase
            .Setup(uc => uc.Execute(fosterFamilyRequest, localAuthorityIds))
            .ThrowsAsync(new ValidationException("Validation failed"));

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);
        // Act
        var actionResult = await _sut.FosterFamily(fosterFamilyRequest);

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;
        badRequestResult.Value.Should().BeOfType<ErrorResponse>();

    }

    [Test]
    public async Task FosterFamily_ShouldReturnBadRequest_When_FosterFamilyRequestIsInvalid()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.FosterFamily(new FosterFamilyRequest()); // Empty request to trigger validation errors 

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Get Foster Family

    [Test]
    public async Task FosterFamily_Get_ShouldReturnNotFound_WhenFosterFamilyDoesNotExist()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        _mockGetFosterFamilyUseCase
            .Setup(uc => uc.Execute("1234"))
            .ReturnsAsync((FosterFamilyResponse?)null);

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.FosterFamily("1234");

        // Assert
        actionResult.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task FosterFamily_Get_ShouldReturnOk_WhenFosterFamilyExists()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };
        var expectedResponse = fosterFamilyRequest.Data;

        _mockGetFosterFamilyUseCase
            .Setup(uc => uc.Execute("1234"))
            .ReturnsAsync(new FosterFamilyResponse
            {
                FosterCarerId = Guid.NewGuid(),
                CarerFirstName = expectedResponse.CarerFirstName,
                CarerLastName = expectedResponse.CarerLastName,
                CarerDateOfBirth = expectedResponse.CarerDateOfBirth,
                CarerNationalInsuranceNumber = expectedResponse.CarerNationalInsuranceNumber,
                HasPartner = expectedResponse.HasPartner,
                PartnerFirstName = expectedResponse.PartnerFirstName,
                PartnerLastName = expectedResponse.PartnerLastName,
                PartnerDateOfBirth = expectedResponse.PartnerDateOfBirth,
                PartnerNationalInsuranceNumber = expectedResponse.PartnerNationalInsuranceNumber,
                ChildFirstName = expectedResponse.ChildFirstName,
                ChildLastName = expectedResponse.ChildLastName,
                ChildDateOfBirth = expectedResponse.ChildDateOfBirth,
                ChildPostCode = expectedResponse.ChildPostCode,
                SubmissionDate = expectedResponse.SubmissionDate
            });

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.FosterFamily("1234");


        // Assert type and status code
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = actionResult as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Assert payload
        objectResult.Value.Should().BeEquivalentTo(expectedResponse);

    }

    [Test]
    public async Task FosterFamily_Get_ShouldReturnBadRequest_WhenNoLocalAuthorityScopeFound()
    {
        // Arrange

        // Setup controller without local authority scope
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var actionResult = await _sut.FosterFamily("1234");

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;

        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = (ErrorResponse)badRequestResult.Value;

        errorResponse.Errors.Should().ContainSingle()
            .Which.Title.Should().Be("No local authority scope found");
    }

    [Test]
    public async Task FosterFamily_Get_ShouldReturnBadRequest_WhenExceptionThrown()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        _mockGetFosterFamilyUseCase
            .Setup(uc => uc.Execute("1234"))
            .ThrowsAsync(new Exception("Unexpected error"));

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.FosterFamily("1234");

        // Assert type and status code
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;

        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = (ErrorResponse)badRequestResult.Value;
    }

    #endregion

    #region Update Foster Family

    [Test]
    public async Task FosterFamily_ShouldReturnOkResponse200_WhenUpdated()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };

        _mockUpdateFosterFamilyUseCase
            .Setup(uc => uc.Execute(myGuid, fosterFamilyUpdateRequest))
            .ReturnsAsync(new FosterFamilyResponse());

        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.UpdateFosterFamily(myGuid, fosterFamilyUpdateRequest);

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)actionResult;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(new FosterFamilyResponse());
    }

    [Test]
    public async Task UpdateFosterFamily_ShouldReturnBadRequest400_WhenRequestIsNull()
    {
        // Arrange
        var localAuthorityIds = new List<int> { 1 };
        SetupControllerWithLocalAuthorityIds(localAuthorityIds);

        // Act
        var actionResult = await _sut.UpdateFosterFamily(myGuid, null!);

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var objectResult = (ObjectResult)actionResult;

        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task UpdateFosterFamily_Get_ShouldReturnBadRequest_WhenNoLocalAuthorityScopeFound()
    {
        // Setup controller without local authority scope
        SetupControllerWithLocalAuthorityIds(new List<int>());

        // Act
        var actionResult = await _sut.UpdateFosterFamily(myGuid, new FosterFamilyUpdateRequest());

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;

        badRequestResult.Value.Should().BeOfType<ErrorResponse>();
        var errorResponse = (ErrorResponse)badRequestResult.Value;

        errorResponse.Errors.Should().ContainSingle()
            .Which.Title.Should().Be("No local authority scope found");
    }
    

    #endregion

    #region Helper methods
    private void SetupControllerWithLocalAuthorityIds(List<int> localAuthorityIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupControllerWithLaAndMatIds(List<int> localAuthorityIds, List<int> multiAcademyTrustIds)
    {
        // Create mock HttpContext with ClaimsPrincipal
        var httpContext = new DefaultHttpContext();
        var claims = SetupSpecificScopeIdClaims(localAuthorityIds, "local_authority");
        claims.AddRange(SetupSpecificScopeIdClaims(multiAcademyTrustIds, "multi_academy_trust"));

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private List<Claim> SetupSpecificScopeIdClaims(List<int> ids, string scopeName)

    {
        var claims = new List<Claim>();

        // Add appropriate scope claims based on ids
        if (ids.Contains(0))
        {
            claims.Add(new Claim("scope", scopeName));
        }
        else
        {
            var scopeValue = string.Join(" ", ids.Select(id => $"{scopeName}:{id}"));
            claims.Add(new Claim("scope", scopeValue));
        }

        return claims;
    }
    #endregion
}