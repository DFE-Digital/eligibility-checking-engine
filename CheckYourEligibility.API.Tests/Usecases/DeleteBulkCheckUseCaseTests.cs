using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteBulkCheckUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<DeleteBulkCheckUseCase>>(MockBehavior.Loose);
        
        _sut = new DeleteBulkCheckUseCase(_mockGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockLogger.VerifyAll();
    }

    private Mock<ICheckEligibility> _mockGateway = null!;
    private Mock<ILogger<DeleteBulkCheckUseCase>> _mockLogger = null!;
    private DeleteBulkCheckUseCase _sut = null!;

    [Test]
    public async Task Execute_Should_Call_DeleteByGroup_On_Gateway_When_User_Has_Permission()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var LocalAuthorityID = 123;
        var allowedLocalAuthorityIDs = new List<int> { LocalAuthorityID };
        
        var bulkCheck = _fixture.Build<BulkCheck>()
            .With(x => x.LocalAuthorityID, LocalAuthorityID)
            .Create();
        
        var response = _fixture.Create<CheckEligibilityBulkDeleteResponse>();
        
        _mockGateway.Setup(s => s.GetBulkCheck(groupId)).ReturnsAsync(bulkCheck);
        _mockGateway.Setup(s => s.DeleteByBulkCheckId(groupId)).ReturnsAsync(response.Data);
        
        // Act
        var result = await _sut.Execute(groupId, allowedLocalAuthorityIDs);

        // Assert
        result.Should().BeEquivalentTo(response);
        _mockGateway.Verify(s => s.GetBulkCheck(groupId), Times.Once);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(groupId), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Admin_To_Delete_Any_Bulk_Check()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var bulkCheckLocalAuthorityID = 123;
        var allowedLocalAuthorityIDs = new List<int> { 0 }; // Admin user
        
        var bulkCheck = _fixture.Build<BulkCheck>()
            .With(x => x.LocalAuthorityID, bulkCheckLocalAuthorityID)
            .Create();
        
        var response = _fixture.Create<CheckEligibilityBulkDeleteResponse>();
        
        _mockGateway.Setup(s => s.GetBulkCheck(groupId)).ReturnsAsync(bulkCheck);
        _mockGateway.Setup(s => s.DeleteByBulkCheckId(groupId)).ReturnsAsync(response.Data);
        
        // Act
        var result = await _sut.Execute(groupId, allowedLocalAuthorityIDs);

        // Assert
        result.Should().BeEquivalentTo(response);
        _mockGateway.Verify(s => s.GetBulkCheck(groupId), Times.Once);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(groupId), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Throw_NotFoundException_When_BulkCheck_Does_Not_Exist()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var allowedLocalAuthorityIDs = new List<int> { 123 };
        
        _mockGateway.Setup(s => s.GetBulkCheck(groupId)).ReturnsAsync((BulkCheck?)null);
        
        // Act
        Func<Task> act = async () => await _sut.Execute(groupId, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowExactlyAsync<NotFoundException>()
            .WithMessage($"Bulk check with ID {groupId} not found.");
        
        _mockGateway.Verify(s => s.GetBulkCheck(groupId), Times.Once);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Throw_InvalidScopeExceeption_When_User_Does_Not_Have_Permission()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var bulkCheckLocalAuthorityID = 123;
        var allowedLocalAuthorityIDs = new List<int> { 456 }; // Different LA
        
        var bulkCheck = _fixture.Build<BulkCheck>()
            .With(x => x.LocalAuthorityID, bulkCheckLocalAuthorityID)
            .Create();
        
        _mockGateway.Setup(s => s.GetBulkCheck(groupId)).ReturnsAsync(bulkCheck);
        
        // Act
        Func<Task> act = async () => await _sut.Execute(groupId, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidScopeException>()
            .WithMessage("Access denied. You can only delete bulk checks for your assigned local authority.");
        
        _mockGateway.Verify(s => s.GetBulkCheck(groupId), Times.Once);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Throw_InvalidScopeException_When_BulkCheck_Has_No_LocalAuthorityID()
    {
        // Arrange
        var groupId = Guid.NewGuid().ToString();
        var allowedLocalAuthorityIDs = new List<int> { 123 };
        
        var bulkCheck = _fixture.Build<BulkCheck>()
            .With(x => x.LocalAuthorityID, (int?)null)
            .Create();
        
        _mockGateway.Setup(s => s.GetBulkCheck(groupId)).ReturnsAsync(bulkCheck);
        
        // Act
        Func<Task> act = async () => await _sut.Execute(groupId, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidScopeException>()
            .WithMessage("Access denied. You can only delete bulk checks for your assigned local authority.");
        
        _mockGateway.Verify(s => s.GetBulkCheck(groupId), Times.Once);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_GroupId_Is_Empty()
    {
        // Arrange
        var allowedLocalAuthorityIDs = new List<int> { 123 };
        
        // Act
        Func<Task> act = async () => await _sut.Execute(string.Empty, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Invalid Request, group ID is required.");
        
        _mockGateway.Verify(s => s.GetBulkCheck(It.IsAny<string>()), Times.Never);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_GroupId_Is_Null()
    {
        // Arrange
        var allowedLocalAuthorityIDs = new List<int> { 123 };
        
        // Act
        Func<Task> act = async () => await _sut.Execute(null!, allowedLocalAuthorityIDs);

        // Assert
        await act.Should().ThrowExactlyAsync<ValidationException>()
            .WithMessage("Invalid Request, group ID is required.");
        
        _mockGateway.Verify(s => s.GetBulkCheck(It.IsAny<string>()), Times.Never);
        _mockGateway.Verify(s => s.DeleteByBulkCheckId(It.IsAny<string>()), Times.Never);
    }

}