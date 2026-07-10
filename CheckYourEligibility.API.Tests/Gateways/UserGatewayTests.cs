using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests;

public class UserGatewayTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private UsersGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _sut = new UsersGateway(
            new NullLoggerFactory(),
            _fakeInMemoryDb);
    }

    [Test]
    public async Task Given_ValidRequest_Should_Return_New_UserId()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var response = await _sut.Create(request);

        // Assert
        response.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Given_ExistingUser_Should_Return_Existing_UserId()
    {
        // Arrange
        var request = CreateRequest();

        var existingUserId = await _sut.Create(request);

        // Act
        var response = await _sut.Create(request);

        // Assert
        response.Should().Be(existingUserId);
    }

    [Test]
    public async Task Given_ExistingUser_Should_Update_LastLogin()
    {
        // Arrange
        var request = CreateRequest();

        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();
        var originalLastLogin = user.LastLogin;

        await Task.Delay(50);

        // Act
        await _sut.Create(request);

        // Assert
        user.LastLogin.Should().BeAfter(originalLastLogin!.Value);
    }

    [Test]
    public async Task Given_Production_Source_Should_Create_Api_User()
    {
        // Arrange
        var request = CreateRequest(
            source: "production-cloudforsomething");

        // Act
        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();

        // Assert
        user.UserType.Should().Be(UserType.API);
    }

    [Test]
    public async Task Given_Api_User_Should_Not_Store_Reference()
    {
        // Arrange
        var request = CreateRequest(
            source: "production-cloudforsomething");

        // Act
        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();

        // Assert
        user.Reference.Should().BeEmpty();
    }

    [Test]
    public async Task Given_FreeSchoolMealsAdmin_Source_Should_Create_Fsm_Admin_User()
    {
        // Arrange
        var request = CreateRequest(
            source: "free-school-meals-admin");

        // Act
        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();

        // Assert
        user.UserType.Should().Be(UserType.FreeSchoolMealsAdmin);
    }

    [Test]
    public async Task Given_ChildcareAdmin_Source_Should_Create_Childcare_Admin_User()
    {
        // Arrange
        var request = CreateRequest(
            source: "childcare-admin");

        // Act
        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();

        // Assert
        user.UserType.Should().Be(UserType.ChildcareAdmin);
    }

    [Test]
    public async Task Given_Invalid_Source_Should_Throw_UserSaveException()
    {
        // Arrange
        var request = CreateRequest(
            source: "some-random-source");

        // Act
        Func<Task> act = () => _sut.Create(request);

        // Assert
        await act.Should().ThrowAsync<UserSaveException>();
    }

    [Test]
    public async Task Given_Invalid_OrganisationType_Should_Throw_UserSaveException()
    {
        // Arrange
        var request = CreateRequest(
            organisationType: "invalid-org-type");

        // Act
        Func<Task> act = () => _sut.Create(request);

        // Assert
        await act.Should().ThrowAsync<UserSaveException>();
    }

    [TestCase("local-authority", OrganisationType.local_authority)]
    [TestCase("multi-academy-trust", OrganisationType.multi_academy_trust)]
    [TestCase("establishment", OrganisationType.establishment)]
    public async Task Given_OrganisationType_Should_Map_Correctly(
        string organisationType,
        OrganisationType expected)
    {
        // Arrange
        var request = CreateRequest(
            organisationType: organisationType);

        // Act
        await _sut.Create(request);

        var user = _fakeInMemoryDb.Users.Single();

        // Assert
        user.OrganisationType.Should().Be(expected);
    }

    private static UserCreateRequest CreateRequest(
        string source = "childcare-admin",
        string organisationType = "local-authority")
    {
        return new UserCreateRequest
        {
            Data = new UserData
            {
                Email = "test@test.com",
                Reference = "ABC123"
            },
            metaData = new CheckMetaData
            {
                Source = source,
                OrganisationType = organisationType,
                OrganisationID = 1
            }
        };
    }
}