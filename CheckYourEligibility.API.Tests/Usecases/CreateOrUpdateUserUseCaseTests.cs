using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

public class CreateOrUpdateUserUseCaseTests
{
    [Test]
    public async Task Execute_Should_Return_UserId()
    {
        // Arrange
        var userGateway = new Mock<IUsers>();

        userGateway
            .Setup(x => x.Create(It.IsAny<UserCreateRequest>()))
            .ReturnsAsync("123");

        var sut = new CreateOrUpdateUserUseCase(
            userGateway.Object);

        // Act
        var response = await sut.Execute(
            new UserCreateRequest());

        // Assert
        response.Data.Should().Be("123");
    }

    [Test]
    public async Task Execute_Should_Call_Create_On_Gateway()
    {
        // Arrange
        var userGateway = new Mock<IUsers>();

        var sut = new CreateOrUpdateUserUseCase(
            userGateway.Object);

        var request = new UserCreateRequest();

        // Act
        await sut.Execute(request);

        // Assert
        userGateway.Verify(
            x => x.Create(request),
            Times.Once);
    }

    [Test]
    public async Task Execute_Should_Propagate_UserSaveException()
    {
        // Arrange
        var userGateway = new Mock<IUsers>();

        userGateway
            .Setup(x => x.Create(It.IsAny<UserCreateRequest>()))
            .ThrowsAsync(new UserSaveException("Failed to save user"));

        var sut = new CreateOrUpdateUserUseCase(
            userGateway.Object);

        // Act
        Func<Task> act = () => sut.Execute(new UserCreateRequest());

        // Assert
        await act.Should().ThrowAsync<UserSaveException>();
    }
}