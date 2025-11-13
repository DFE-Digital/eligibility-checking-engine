using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetBulkUploadResultsUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetBulkUploadResultsUseCase>>(MockBehavior.Loose);
        _sut = new GetBulkUploadResultsUseCase(_mockCheckGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<ICheckEligibility> _mockCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<GetBulkUploadResultsUseCase>> _mockLogger;
    private GetBulkUploadResultsUseCase _sut;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Arrange
        var allowedLocalAuthorityIds = new List<int> { 201 };
        
        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, group ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 201;
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync((IList<CheckEligibilityItem>)null!);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_results()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        result.Data.Should().BeEquivalentTo(resultItems);
    }

    [Test]
    public async Task Execute_calls_gateway_GetBulkCheckResults_with_correct_guid()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockCheckGateway.Verify(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid), Times.Once);
    }

    [Test]
    public async Task Execute_calls_auditService_AuditDataGet_with_correct_parameters()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid), Times.Once);
    }

    [Test]
    public async Task Execute_throws_UnauthorizedAccessException_when_user_not_authorized_for_local_authority()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 }; // User only has access to LA 201
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 305; // But bulk check belongs to LA 305
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"You do not have permission to access bulk check {guid}");
    }

    [Test]
    public async Task Execute_succeeds_when_user_is_admin()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 0 }; // Admin access (0 means all)
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = 305; // Any local authority
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEquivalentTo(resultItems);
    }

    [Test]
    public async Task Execute_throws_UnauthorizedAccessException_when_bulk_check_has_null_local_authority()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIds = new List<int> { 201 }; // User has access to LA 201
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityId = null; // But bulk check has no local authority set
        
        _mockCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIds);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"You do not have permission to access bulk check {guid}");
    }
}