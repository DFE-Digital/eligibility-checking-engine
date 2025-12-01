using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateApplicationUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new UpdateApplicationUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
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
    private UpdateApplicationUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_Should_Return_Null_When_Response_Is_Null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!))
            .ReturnsAsync((ApplicationUpdateResponse)null!);

        // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task Execute_Should_Call_UpdateApplication_On_ApplicationGateway()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var response = _fixture.Create<ApplicationUpdateResponse>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.UpdateApplication(guid, model.Data!), Times.Once);
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_AllowedLocalAuthorityIds_Contains_Zero()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var response = _fixture.Create<ApplicationUpdateResponse>();
        var allowedLocalAuthorityIds = new List<int> { 0 }; // 0 means all authorities
        var localAuthorityId = 5; // Any authority ID

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_LocalAuthorityId_Is_In_AllowedList()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var response = _fixture.Create<ApplicationUpdateResponse>();
        var localAuthorityId = 2;
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid))
            .ReturnsAsync(_fixture.Create<string>()); // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthorityId_Is_Not_In_AllowedList()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var localAuthorityId = 5;
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(guid, model, allowedLocalAuthorityIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage(
            "You do not have permission to create applications for this establishment's local authority");
    }

    [Test]
    public async Task Execute_Should_Call_GetLocalAuthorityIdForApplication_Before_Authorization_Check()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var response = _fixture.Create<ApplicationUpdateResponse>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForApplication(guid), Times.Once);
        result.Should().NotBeNull();
    }

    [Test]
    public async Task Execute_Should_Create_Audit_Entry_After_Successful_Update()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var model = _fixture.Create<ApplicationUpdateRequest>();
        var response = _fixture.Create<ApplicationUpdateResponse>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.UpdateApplication(guid, model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, model, allowedLocalAuthorityIds);

        // Assert
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Application, guid), Times.Once);
        result.Should().NotBeNull();
    }
}