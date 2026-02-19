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
        _mockBulkCheckGateway = new Mock<IBulkCheck>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetBulkUploadResultsUseCase>>(MockBehavior.Loose);
        _sut = new GetBulkUploadResultsUseCase(_mockBulkCheckGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockBulkCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IBulkCheck> _mockBulkCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<GetBulkUploadResultsUseCase>> _mockLogger;
    private GetBulkUploadResultsUseCase _sut;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Arrange
        var allowedLocalAuthorityIDs = new List<int> { 201 };
        
        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, group ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 201;
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync((IList<CheckEligibilityItem>)null!);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_results()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        result.Data.Should().BeEquivalentTo(resultItems);
    }

    [Test]
    public async Task Execute_calls_gateway_GetBulkCheckResults_with_correct_guid()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        _mockBulkCheckGateway.Verify(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid), Times.Once);
    }

    [Test]
    public async Task Execute_calls_auditService_AuditDataGet_with_correct_parameters()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 };
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 201;
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid,null), Times.Once);
    }

    [Test]
    public async Task Execute_throws_UnauthorizedAccessException_when_user_not_authorized_for_local_authority()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 }; // User only has access to LA 201
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 305; // But bulk check belongs to LA 305
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"You do not have permission to access bulk check {guid}");
    }

    [Test]
    public async Task Execute_succeeds_when_user_is_admin()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 0 }; // Admin access (0 means all)
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = 305; // Any local authority
        
        var resultItems = _fixture.CreateMany<CheckEligibilityItem>().ToList();
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheckResults<IList<CheckEligibilityItem>>(guid))
            .ReturnsAsync(resultItems);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.CheckBulkResults, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEquivalentTo(resultItems);
    }

    [Test]
    public async Task Execute_throws_UnauthorizedAccessException_when_bulk_check_has_null_local_authority()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var allowedLocalAuthorityIDs = new List<int> { 201 }; // User has access to LA 201
        var bulkCheck = _fixture.Create<BulkCheck>();
        bulkCheck.LocalAuthorityID = null; // But bulk check has no local authority set
        
        _mockBulkCheckGateway.Setup(s => s.GetBulkCheck(guid))
            .ReturnsAsync(bulkCheck);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"You do not have permission to access bulk check {guid}");
    }
}