using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

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
}