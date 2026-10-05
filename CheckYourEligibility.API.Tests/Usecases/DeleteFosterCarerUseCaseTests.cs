using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteFosterCarerUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private DeleteFosterCarerUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new DeleteFosterCarerUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Carer_Id_Is_Empty()
    {
        // Act
        Func<Task> act = () => _sut.Execute(Guid.Empty, 201);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Delete_Carer()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        _mockGateway
            .Setup(x => x.DeleteFosterCarer(fosterCarerId, 201))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(fosterCarerId, 201);

        // Assert
        _mockGateway.Verify(x => x.DeleteFosterCarer(fosterCarerId, 201), Times.Once);
    }
}
