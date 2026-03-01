using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests;

public class LocalAuthoritiesControllerTests : TestBase.TestBase
{
    private Fixture _fixture;
    private Mock<ILocalAuthority> _mockLocalAuthority;
    private Mock<IAudit> _mockAudit;
    private LocalAuthoritiesController _sut;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _mockLocalAuthority = new Mock<ILocalAuthority>(MockBehavior.Strict);
        _mockAudit = new Mock<IAudit>(MockBehavior.Strict);

        _sut = new LocalAuthoritiesController(
            _mockLocalAuthority.Object,
            _mockAudit.Object
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockLocalAuthority.VerifyAll();
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task Given_GetSettings_When_LocalAuthorityExists_ReturnsOkWithCorrectFlag(bool flag)
    {
        // Arrange
        var laCode = _fixture.Create<int>();

        _mockLocalAuthority
            .Setup(x => x.GetLocalAuthorityById(laCode))
            .ReturnsAsync(new LocalAuthority
            {
                LocalAuthorityID = laCode,
                LaName = _fixture.Create<string>(),
                SchoolCanReviewEvidence = flag
            });

        // Act
        var result = await _sut.GetSettings(laCode);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var payload = ok!.Value as LocalAuthoritySettingsResponse;
        payload.Should().NotBeNull();
        payload!.SchoolCanReviewEvidence.Should().Be(flag);
    }

    [Test]
    public async Task Given_GetSettings_When_LocalAuthorityNotFound_ReturnsNotFoundWithErrorResponse()
    {
        // Arrange
        var laCode = _fixture.Create<int>();

        _mockLocalAuthority
            .Setup(x => x.GetLocalAuthorityById(laCode))
            .ReturnsAsync((LocalAuthority)null);

        // Act
        var result = await _sut.GetSettings(laCode);

        // Assert
        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();

        var payload = notFound!.Value as ErrorResponse;
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNullOrEmpty();
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
}