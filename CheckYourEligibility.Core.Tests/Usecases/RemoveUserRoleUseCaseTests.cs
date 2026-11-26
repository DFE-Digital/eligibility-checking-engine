using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class RemoveUserRoleUseCaseTests
{
    private Mock<IUsers> _userGateway = null!;
    private RemoveUserRoleUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _userGateway = new Mock<IUsers>(MockBehavior.Strict);
        _sut = new RemoveUserRoleUseCase(_userGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _userGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        UserRoleName userRole = UserRoleName.Support_TestRole;
        _userGateway.Setup(x => x.RemoveUserRole(userId, userRole)).ReturnsAsync(true);

        // Act
        var result = await _sut.Execute(userId, UserRoleName.Support_TestRole);

        // Assert
        result.Should().BeTrue();
    }

}