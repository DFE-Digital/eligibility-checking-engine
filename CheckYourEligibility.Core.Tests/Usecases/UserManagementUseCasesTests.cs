using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class UserManagementUseCasesTests
{
    private Mock<IUsers> _gateway = null!;

    [SetUp]
    public void Setup() => _gateway = new Mock<IUsers>(MockBehavior.Strict);

    [TearDown]
    public void Teardown() => _gateway.VerifyAll();

    [TestCase(0, 0, 1, 10)]
    [TestCase(3, 500, 3, 100)]
    public async Task Search_Should_Normalise_Paging_And_Return_Response(int page, int size, int expectedPage, int expectedSize)
    {
        var users = new List<User> { new() { UserID = "1" } };
        _gateway.Setup(x => x.SearchUsers("a", UserType.API, expectedPage, expectedSize)).ReturnsAsync((users, 7));

        var result = await new SearchUsersUseCase(_gateway.Object).Execute("a", UserType.API, page, size);

        result.PageNumber.Should().Be(expectedPage);
        result.PageSize.Should().Be(expectedSize);
        result.TotalNumberOfRecords.Should().Be(7);
        result.Data.Should().BeEquivalentTo(users);
    }

    [Test]
    public async Task Get_Should_Return_User_And_Roles()
    {
        var user = new User { UserID = "1" };
        IList<UserRole> roles = [new UserRole { UserId = "1", RoleName = UserRoleName.Support_TestRole, UserRoleId = Guid.NewGuid() }];
        _gateway.Setup(x => x.GetUser("1")).ReturnsAsync((user, roles));

        var result = await new GetUserUseCase(_gateway.Object).Execute("1");

        result.Data.Should().BeSameAs(user);
        result.Roles.Should().ContainSingle().Which.RoleName.Should().Be(UserRoleName.Support_TestRole);
    }

    [Test]
    public async Task Update_Should_Call_Gateway()
    {
        _gateway.Setup(x => x.UpdateUserEnabled("1", false)).Returns(Task.CompletedTask);

        await new UpdateUserEnabledUseCase(_gateway.Object).Execute("1", false);
    }
}
