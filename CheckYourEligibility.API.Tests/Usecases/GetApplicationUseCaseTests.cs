using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetApplicationUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new GetApplicationUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockApplicationGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private GetApplicationUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_Should_Return_Null_When_Response_Is_Null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplication(guid)).ReturnsAsync((ApplicationResponse)null);

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task Execute_Should_Call_GetApplication_On_ApplicationGateway()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var response = _fixture.Create<ApplicationResponse>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplication(guid)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForApplication(guid), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplication(guid), Times.Once);
        result.Data.Should().Be(response);
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_AllowedLocalAuthorityIds_Contains_Zero()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var response = _fixture.Create<ApplicationResponse>();
        var localAuthorityId = 999; // This doesn't match any specific allowed authority
        var allowedLocalAuthorityIds = new List<int> { 0 }; // 0 means all authorities are allowed

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplication(guid)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().Be(response);
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_LocalAuthorityId_Is_In_AllowedList()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var response = _fixture.Create<ApplicationResponse>();
        var localAuthorityId = 2;
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplication(guid)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().Be(response);
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthority_Not_Allowed()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityId = 999; // This doesn't match any allowed authority
        var restrictedAuthorities = new List<int> { 1, 2, 3 }; // Specific authorities only, not including 0 (all)

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, restrictedAuthorities);

        // Assert
        act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not have permission to access applications for this establishment's local authority");
    }

    [Test]
    public async Task Execute_Should_Call_GetLocalAuthorityIdForApplication_Before_Authorization_Check()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var response = _fixture.Create<ApplicationResponse>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplication(guid)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForApplication(guid), Times.Once);
        result.Should().NotBeNull();
    }
}