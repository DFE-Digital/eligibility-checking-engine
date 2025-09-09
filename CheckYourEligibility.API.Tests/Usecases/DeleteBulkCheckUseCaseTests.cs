using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework.Internal;

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
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockLogger.VerifyAll();
    }

    private Mock<ICheckEligibility> _mockGateway;
    private Mock<ILogger<DeleteBulkCheckUseCase>> _mockLogger;
    private DeleteBulkCheckUseCase _sut;
    private Fixture _fixture;

    [Test]
    public async Task Execute_Should_Call_DeleteByGroup_On_gateway()
    {
        // Arrange
        var response = _fixture.Create<CheckEligibilityBulkDeleteResponse>();
        _mockGateway.Setup(s => s.DeleteByGroup(It.IsAny<string>())).ReturnsAsync(response);
        
        // Act
        await _sut.Execute(Guid.NewGuid().ToString());

        // Assert
        _mockGateway.Verify(s => s.DeleteByGroup(It.IsAny<string>()), Times.Once);
    }


    [Test]
    public async Task Execute_Should_Not_Call_DeleteByGroup_On_gateway()
    {
        // Arrange

        // Act
        Func<Task> act = async () => await _sut.Execute(string.Empty);

        // Assert
        act.Should().ThrowExactlyAsync<ValidationException>();
    }

}