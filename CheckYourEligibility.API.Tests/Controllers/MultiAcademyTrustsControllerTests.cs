using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests;

public class MultiAcademyTrustsControllerTests : TestBase.TestBase
{
    private Fixture _fixture;
    private Mock<IMultiAcademyTrust> _mockMultiAcademyTrust;
    private Mock<IAudit> _mockAudit;
    private IConfiguration _configuration;
    private MultiAcademyTrustsController _sut;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _mockMultiAcademyTrust = new Mock<IMultiAcademyTrust>(MockBehavior.Strict);
        _mockAudit = new Mock<IAudit>(MockBehavior.Strict);

        var configData = new Dictionary<string, string>
        {
            { "Jwt:Scopes:multi_academy_trust", "multi_academy_trust" },
            { "Jwt:Scopes:admin", "admin" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new MultiAcademyTrustsController(
            _mockMultiAcademyTrust.Object,
            _mockAudit.Object,
            _configuration
        );
    }

    [TearDown]
    public void Teardown()
    {
        _mockMultiAcademyTrust.VerifyAll();
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
    public async Task Given_GetSettings_When_MultiAcademyTrustExists_ReturnsOkWithCorrectFlag(bool flag)
    {
        // Arrange
        var matId = _fixture.Create<int>();

        _mockMultiAcademyTrust
            .Setup(x => x.GetMultiAcademyTrustById(matId))
            .ReturnsAsync(new MultiAcademyTrust
            {
                MultiAcademyTrustID = matId,
                Name = _fixture.Create<string>(),
                AcademyCanReviewEvidence = flag
            });

        // Act
        var result = await _sut.GetSettings(matId);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var payload = ok!.Value as MultiAcademyTrustSettingsResponse;
        payload.Should().NotBeNull();
        payload!.AcademyCanReviewEvidence.Should().Be(flag);
    }

    [Test]
    public async Task Given_GetSettings_When_MultiAcademyTrustNotFound_ReturnsNotFoundWithErrorResponse()
    {
        // Arrange
        var matId = _fixture.Create<int>();

        _mockMultiAcademyTrust
            .Setup(x => x.GetMultiAcademyTrustById(matId))
            .ReturnsAsync((MultiAcademyTrust)null);

        // Act
        var result = await _sut.GetSettings(matId);

        // Assert
        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();

        var payload = notFound!.Value as ErrorResponse;
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNullOrEmpty();
        payload.Errors.First().Title.Should().Be($"Multi Academy Trust '{matId}' not found");
    }

    [Test]
    public async Task Given_UpdateSettings_When_Admin_Should_Return200()
    {
        // Arrange
        const int matId = 894;
        var request = new MultiAcademyTrustSettingsUpdateRequest { AcademyCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "admin"));

        _mockMultiAcademyTrust
            .Setup(x => x.UpdateAcademyCanReviewEvidence(matId, true))
            .ReturnsAsync(new MultiAcademyTrust
            {
                MultiAcademyTrustID = matId,
                AcademyCanReviewEvidence = true
            });

        // Act
        var result = await _sut.UpdateSettings(matId, request);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var body = ok!.Value as MultiAcademyTrustSettingsResponse;
        body.Should().NotBeNull();
        body!.AcademyCanReviewEvidence.Should().BeTrue();
    }

    [Test]
    public async Task Given_UpdateSettings_When_MatScopeMatches_Should_Return200()
    {
        // Arrange
        const int matId = 894;
        var request = new MultiAcademyTrustSettingsUpdateRequest { AcademyCanReviewEvidence = false };

        SetUserClaims(new Claim("scope", "multi_academy_trust:894"));

        _mockMultiAcademyTrust
            .Setup(x => x.UpdateAcademyCanReviewEvidence(matId, false))
            .ReturnsAsync(new MultiAcademyTrust
            {
                MultiAcademyTrustID = matId,
                AcademyCanReviewEvidence = false
            });

        // Act
        var result = await _sut.UpdateSettings(matId, request);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var body = ok!.Value as MultiAcademyTrustSettingsResponse;
        body.Should().NotBeNull();
        body!.AcademyCanReviewEvidence.Should().BeFalse();
    }

    [Test]
    public async Task Given_UpdateSettings_When_NonAdminAndMatScopeDoesNotMatch_Should_Return403()
    {
        // Arrange
        const int requestedMatId = 894;
        var request = new MultiAcademyTrustSettingsUpdateRequest { AcademyCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "multi_academy_trust:123"));

        // Act
        var result = await _sut.UpdateSettings(requestedMatId, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Test]
    public async Task Given_UpdateSettings_When_NonAdminAndGeneralMatScope_Should_Return403()
    {
        // Arrange
        const int requestedMatId = 894;
        var request = new MultiAcademyTrustSettingsUpdateRequest { AcademyCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "multi_academy_trust:0"));

        // Act
        var result = await _sut.UpdateSettings(requestedMatId, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Test]
    public async Task Given_UpdateSettings_When_MultiAcademyTrustNotFound_Should_Return404()
    {
        // Arrange
        const int matId = 894;
        var request = new MultiAcademyTrustSettingsUpdateRequest { AcademyCanReviewEvidence = true };

        SetUserClaims(new Claim("scope", "admin"));

        _mockMultiAcademyTrust
            .Setup(x => x.UpdateAcademyCanReviewEvidence(matId, true))
            .ReturnsAsync((MultiAcademyTrust?)null);

        // Act
        var result = await _sut.UpdateSettings(matId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}