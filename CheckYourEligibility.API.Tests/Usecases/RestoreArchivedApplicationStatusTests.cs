using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class RestoreArchivedApplicationStatusUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new RestoreArchivedApplicationStatusUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
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
    private RestoreArchivedApplicationStatusUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_Should_Call_RestoreArchivedStatus_On_ApplicationGateway()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationStatusRestoreResponse>();
        var localAuthorityId = 1;

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.RestoreArchivedApplicationStatus(guid))
            .ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.RestoreArchivedApplicationStatus(guid), Times.Once);
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Return_Null_When_Response_Is_Null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var localAuthorityId = 1;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.RestoreArchivedApplicationStatus(guid))
            .ReturnsAsync((ApplicationStatusRestoreResponse)null!);
        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);
        // Assert
        result.Should().BeNull();

    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthorityId_Not_Allowed()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 2, 3, 4 };
        var localAuthorityId = 1;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        // Act
        Func<Task> act = async () => { await _sut.Execute(guid, allowedLocalAuthorityIds); };
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not have permission to update applications for this establishment's local authority");
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_LocalAuthorityId_Is_Zero()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 0 };
        var response = _fixture.Create<ApplicationStatusRestoreResponse>();
        var localAuthorityId = 1;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.RestoreArchivedApplicationStatus(guid))
            .ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.RestoreArchivedApplicationStatus(guid), Times.Once);
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Create_Audit_Entry_After_Successful_Restore()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationStatusRestoreResponse>();
        var localAuthorityId = 1;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.RestoreArchivedApplicationStatus(guid))
            .ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Application, guid, null), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Access_When_LocalAuthorityId_Is_In_Allowed_List()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationStatusRestoreResponse>();
        var localAuthorityId = 2;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.RestoreArchivedApplicationStatus(guid))
            .ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, guid, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.RestoreArchivedApplicationStatus(guid), Times.Once);
        result!.Data.Should().Be(response.Data);
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthorityId_Not_In_Allowed_List()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 3, 4, 5 };
        var localAuthorityId = 2;
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(localAuthorityId);
        // Act
        Func<Task> act = async () => { await _sut.Execute(guid, allowedLocalAuthorityIds); };
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not have permission to update applications for this establishment's local authority");
    }
}