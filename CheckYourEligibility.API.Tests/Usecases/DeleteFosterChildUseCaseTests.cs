using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private DeleteFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new DeleteFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Child_Id_Is_Empty()
    {
        // Act
        Func<Task> act = () => _sut.Execute(Guid.Empty, 201);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Delete_Child()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();

        _mockGateway
            .Setup(x => x.DeleteFosterChild(fosterChildId, 201))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(fosterChildId, 201);

        // Assert
        _mockGateway.Verify(x => x.DeleteFosterChild(fosterChildId, 201), Times.Once);
    }
}
