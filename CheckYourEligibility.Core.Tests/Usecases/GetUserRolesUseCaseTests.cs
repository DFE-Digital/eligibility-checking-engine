using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class GetUserRolesUseCaseTests
{
    private Mock<IUsers> _mockGateway = null!;
    private GetUserRolesUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IUsers>(MockBehavior.Strict);
        _sut = new GetUserRolesUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        List<UserRole> data = [
            new UserRole(){
                RoleName = UserRoleName.Support_TestRole,
                UserId=id,
                UserRoleId = Guid.NewGuid()
            }
        ];

        UserRolesResponse expected = new()
        {
            Data = [
                new UserRoleItemResponse(){
                    RoleName = data[0].RoleName,
                    UserId=data[0].UserId,
                    UserRoleId = data[0].UserRoleId
                }
             ]
        };

        _mockGateway.Setup(x => x.GetUserRoles(id)).ReturnsAsync(data);

        // Act
        var result = await _sut.Execute(id);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

}