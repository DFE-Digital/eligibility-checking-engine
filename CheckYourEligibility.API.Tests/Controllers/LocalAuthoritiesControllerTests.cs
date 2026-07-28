using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests;

public class LocalAuthoritiesControllerTests : TestBase.TestBase
{
    private Mock<ILocalAuthority> _mockLocalAuthority;

    private Mock<ILocalAuthoritiesUseCase> _localAuthoritiesUseCase;
    private Mock<IGetEstablishmentsByLocalAuthorityIdUseCase> _getEstablismentsByLocalAuthorityId;
    private Mock<IAudit> _mockAudit;
    private LocalAuthoritiesController _sut;

    [SetUp]
    public void Setup()
    {
        _mockLocalAuthority = new Mock<ILocalAuthority>(MockBehavior.Strict);
        _localAuthoritiesUseCase = new Mock<ILocalAuthoritiesUseCase>(MockBehavior.Strict);
        _getEstablismentsByLocalAuthorityId = new Mock<IGetEstablishmentsByLocalAuthorityIdUseCase>(MockBehavior.Strict);
        _mockAudit = new Mock<IAudit>(MockBehavior.Strict);

        _sut = new LocalAuthoritiesController(
            _localAuthoritiesUseCase.Object,
            _mockAudit.Object,
            _mockLocalAuthority.Object,
            _getEstablismentsByLocalAuthorityId.Object
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockLocalAuthority.VerifyAll();
        _localAuthoritiesUseCase.VerifyAll();
        _mockAudit.VerifyAll();
    }

    private void SetUserClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Test]
    public async Task GetSettings_WhenLocalAuthorityExists_ReturnsOkWithThreeEligibilityPoliciesAndSchoolCanReviewEvidenceFlag()
    {
        // Arrange
        var laCode = 123;
        var expectedResponse = new LocalAuthoritySettingsResponse
        {
            SchoolCanReviewEvidence = true,
            EligibilityPolicies = new List<EligibilityPolicyResponse>
            {
                new EligibilityPolicyResponse(CheckEligibilityType.FreeSchoolMeals.ToString(),EligibilityCriteria.standard.ToString()),
                new EligibilityPolicyResponse (CheckEligibilityType.EarlyYearPupilPremium.ToString(), EligibilityCriteria.standard.ToString()),
                new EligibilityPolicyResponse (CheckEligibilityType.TwoYearOffer.ToString(), EligibilityCriteria.standard.ToString())
            }
        };

        _localAuthoritiesUseCase
            .Setup(x => x.Execute(laCode))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.GetSettings(laCode);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        var response = okResult.Value as LocalAuthoritySettingsResponse;
        response.Should().NotBeNull();

        response.SchoolCanReviewEvidence.Should().BeTrue();
        response.EligibilityPolicies.Should().HaveCount(3);
        response.EligibilityPolicies[0].CheckType.Should().Be(expectedResponse.EligibilityPolicies[0].CheckType);
        response.EligibilityPolicies[0].EligibilityCriteria.Should().Be(expectedResponse.EligibilityPolicies[0].EligibilityCriteria);
        response.EligibilityPolicies[1].CheckType.Should().Be(expectedResponse.EligibilityPolicies[1].CheckType);
        response.EligibilityPolicies[1].EligibilityCriteria.Should().Be(expectedResponse.EligibilityPolicies[1].EligibilityCriteria);
        response.EligibilityPolicies[2].CheckType.Should().Be(expectedResponse.EligibilityPolicies[2].CheckType);
        response.EligibilityPolicies[2].EligibilityCriteria.Should().Be(expectedResponse.EligibilityPolicies[2].EligibilityCriteria);
    }
    [Test]
    public async Task Given_GetSettings_When_LocalAuthorityNotFound_ReturnsNotFoundWithErrorResponse()
    {
        // Arrange
        var laCode = _fixture.Create<int>();

        _localAuthoritiesUseCase
            .Setup(x => x.Execute(laCode))
            .ThrowsAsync(new NotFoundException());

        // Act
        var result = await _sut.GetSettings(laCode);

        // Assert
        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();

        var payload = notFound!.Value as ErrorResponse;
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNullOrEmpty();
        payload.Errors.First().Status.Should().Be(StatusCodes.Status404NotFound);
        payload.Errors.First().Title.Should().Be($"Local authority '{laCode}' not found");
    }

    [Test]
    public async Task Given_UpdateSettings_When_Admin_Should_Return200()
    {
        // Arrange
        const int laCode = 894;
        var request = new LocalAuthoritySettingsUpdateRequest { SchoolCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "admin"));

        _mockLocalAuthority
            .Setup(x => x.UpdateSchoolCanReviewEvidence(laCode, true))
            .ReturnsAsync(new LocalAuthority { LocalAuthorityID = laCode, SchoolCanReviewEvidence = true });

        // Act
        var result = await _sut.UpdateSettings(laCode, request);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var body = ok!.Value as LocalAuthoritySettingsResponse;
        body.Should().NotBeNull();
        body!.SchoolCanReviewEvidence.Should().BeTrue();
    }

    [Test]
    public async Task Given_UpdateSettings_When_NonAdminAndScopeDoesNotMatch_Should_Return403()
    {
        // Arrange
        const int requestedLa = 894;
        var request = new LocalAuthoritySettingsUpdateRequest { SchoolCanReviewEvidence = true };

        // Token says local_authority:123, but trying to patch 894
        SetUserClaims(new Claim("scope", "local_authority:123"));

        // Act
        var result = await _sut.UpdateSettings(requestedLa, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Test]
    public async Task Given_UpdateSettings_When_LaNotFound_Should_Return404()
    {
        // Arrange
        const int laCode = 894;
        var request = new LocalAuthoritySettingsUpdateRequest { SchoolCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "admin"));

        _mockLocalAuthority
            .Setup(x => x.UpdateSchoolCanReviewEvidence(laCode, true))
            .ReturnsAsync((LocalAuthority?)null);

        // Act
        var result = await _sut.UpdateSettings(laCode, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task Given_GetEstablishmentsByLocalAuthorityId_When_ValidId_Returns200WithData()
    {
        // Arrange
        var laCode = _fixture.Create<int>();

        var response = new EstablishmentResponse
        {
            Data = new List<EstablishmentResponseItem>
        {
            new EstablishmentResponseItem
            {
                URN = 1,
                Name = "Test School"
            }
        }
        };

        _getEstablismentsByLocalAuthorityId
            .Setup(x => x.Execute(laCode))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.GetEstablishmentsByLocalAuthorityId(laCode);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var payload = ok!.Value as EstablishmentResponse;
        payload.Should().NotBeNull();

        payload!.Data.Should().NotBeNull();
        payload.Data.Count.Should().Be(1);
        payload.Data.First().Name.Should().Be("Test School");

        _getEstablismentsByLocalAuthorityId.Verify(
            x => x.Execute(laCode),
            Times.Once
        );
    }

    [Test]
    public async Task Given_GetEstablishmentsByLocalAuthorityId_When_NotFound_ThrowsException_Returns404()
    {
        // Arrange
        var laCode = _fixture.Create<int>();

        _getEstablismentsByLocalAuthorityId
            .Setup(x => x.Execute(laCode))
            .ThrowsAsync(new NotFoundException("Not found"));

        // Act
        var result = await _sut.GetEstablishmentsByLocalAuthorityId(laCode);

        // Assert
        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();

        var payload = notFound!.Value as ErrorResponse;
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNullOrEmpty();

        payload.Errors.First().Status
            .Should().Be(StatusCodes.Status404NotFound);

        payload.Errors.First().Title
            .Should().Be($"Local authority '{laCode}' not found.");
    }
}