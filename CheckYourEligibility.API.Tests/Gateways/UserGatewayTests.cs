// Ignore Spelling: Levenshtein

using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using String = string;

namespace CheckYourEligibility.API.Tests;

public class UserGatewayTests : TestBase.TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IMapper _mapper;
    private UsersGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(UserGatewayTests),
                InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);
        _fakeInMemoryDb.Database.EnsureDeleted();
        _fakeInMemoryDb.Database.EnsureCreated();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();

        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" },
            { "HashCheckDays", "7" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();

        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _sut = new UsersGateway(
            new NullLoggerFactory(),
            _fakeInMemoryDb,
            _mapper);
    }

    [TearDown]
    public void Teardown()
    {
        _fakeInMemoryDb.Users.RemoveRange(_fakeInMemoryDb.Users);
        _fakeInMemoryDb.SaveChanges();
    }

    [TestCase(UserType.FreeSchoolMealsAdmin)]
    [TestCase(UserType.FreeSchoolMealsParent)]
    [TestCase(UserType.ChildcareAdmin)]
    public async Task Given_Existing_Portal_User_Should_Update_LastLogin(UserType userType)
    {
        // Arrange
        var existingUser = new User
        {
            UserID = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            Reference = "",
            UserName = "test@test.com",
            UserType = userType,
            OrganisationType = Domain.Enums.OrganisationType.local_authority,
            OrganisationId = 123,
            LastLogin = DateTime.UtcNow.AddDays(-1)
        };

        _fakeInMemoryDb.Users.Add(existingUser);
        await _fakeInMemoryDb.SaveChangesAsync();

        var originalLastLogin = existingUser.LastLogin;

        var request = new UserCreateRequest
        {
            Data = new()
            {
                Email = "test@test.com",
                Reference = ""
            },
            MetaData = new()
            {
                Source = userType.ToString(),
                UserName = "test@test.com",
                OrganisationType = "local_authority",
                OrganisationID = 123
            }
        };

        // Act
        await _sut.CreateOrUpdateUser(request);

        // Assert
        _fakeInMemoryDb.Users.Should().HaveCount(1);

        var updatedUser = _fakeInMemoryDb.Users.Single();

        updatedUser.UserID.Should().Be(existingUser.UserID);
        updatedUser.LastLogin.Should().BeAfter(originalLastLogin!.Value);
    }


    [Test]
    public async Task Given_New_Api_User_Should_Create_User()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Data = new() { Email = "", Reference = "" },
            MetaData = new()
            {
                Source = UserType.API.ToString(),
                UserName = "production-something",
                OrganisationType = "local_authority",
                OrganisationID = 123
            }
        };

        // Act
        await _sut.CreateOrUpdateUser(request);

        // Assert
        var user = _fakeInMemoryDb.Users.First();

        user.UserName.Should().Be("production-something");
        user.UserType.Should().Be(UserType.API);
        user.OrganisationId.Should().Be(123);
    }


    [TestCase(Domain.Enums.OrganisationType.local_authority)]
    [TestCase(Domain.Enums.OrganisationType.multi_academy_trust)]
    [TestCase(Domain.Enums.OrganisationType.establishment)]
    [Test]
    public async Task Given_Existing_Api_User_Should_Update_LastLogin(Domain.Enums.OrganisationType organisationType)
    {
        // Arrange
        var existingUser = new User
        {
            Email = string.Empty,
            Reference = string.Empty,
            UserID = Guid.NewGuid().ToString(),
            UserName = "production-something",
            UserType = UserType.API,
            OrganisationType = organisationType,
            OrganisationId = 123,
            LastLogin = DateTime.UtcNow.AddDays(-1)
        };

        _fakeInMemoryDb.Users.Add(existingUser);
        await _fakeInMemoryDb.SaveChangesAsync();

        var originalLastLogin = existingUser.LastLogin;

        var request = new UserCreateRequest
        {
            Data = new(),
            MetaData = new()
            {
                Source = UserType.API.ToString(),
                UserName = "production-something",
                OrganisationType = organisationType.ToString(),
                OrganisationID = 123
            }
        };

        // Act
        await _sut.CreateOrUpdateUser(request);

        // Assert
        var updatedUser = _fakeInMemoryDb.Users.Single();

        updatedUser.LastLogin.Should().NotBe(originalLastLogin);
        updatedUser.LastLogin.Should().BeAfter(originalLastLogin!.Value);

        _fakeInMemoryDb.Users.Should().HaveCount(1);
    }


    [Test]
    public void Given_Existing_FSMParent_User_Should_Return_Guid()
    {
        // Arrange
        var request = _fixture.Build<UserCreateRequest>()
        .With(x => x.MetaData, new CheckMetaData
        {
            OrganisationType = "local_authority",
            OrganisationID = 123,
            UserName = "testuser"
        })
        .Create();

        // Act
        var response = _sut.CreateOrUpdateFSMParentUser(request);

        // Assert
        response.Result.Should().BeOfType<String>();
    }

    [Test]
    public void Given_validRequest_FSMParent_User_Should_Return_New_Guid()
    {
        // Arrange
        var request = _fixture.Build<UserCreateRequest>()
        .With(x => x.MetaData, new CheckMetaData
        {
            OrganisationType = "local_authority",
            OrganisationID = 123,
            UserName = "testuser"
        })
        .Create();

        // Act
        var response = _sut.CreateOrUpdateFSMParentUser(request);

        // Assert
        response.Result.Should().BeOfType<String>();
    }

    [Test]
    public void Given_DB_Add_Should_ThrowException()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        var svc = new UsersGateway(new NullLoggerFactory(), db.Object, _mapper);
        db.Setup(x => x.Users.Add(It.IsAny<User>())).Throws(new Exception());
        var request = _fixture.Create<UserCreateRequest>();

        // Act 
        Func<Task> act = async () => await svc.CreateOrUpdateFSMParentUser(request);

        // Assert
        act.Should().ThrowExactlyAsync<Exception>();
    }

    [Test]
    public void Given_DB_Add_Should_ThrowDbUpdateException()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        var svc = new UsersGateway(new NullLoggerFactory(), db.Object, _mapper);
        var ex = new DbUpdateException("",
            new Exception(
                "Cannot insert duplicate key row in object 'dbo.Users' with unique index 'IX_Users_Email_Reference'."));

        db.Setup(x => x.Users.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ThrowsAsync(ex);
        var existingUser = _fixture.Create<User>();

        var request = new UserCreateRequest
        {
            Data = new()
            {
                Email = existingUser.Email,
                Reference = existingUser.Reference
            },

            MetaData = new()
            {
                Source = existingUser.UserType.ToString(),
                UserName = existingUser.UserName,
                OrganisationID = existingUser.OrganisationId,
                OrganisationType = existingUser.OrganisationType.ToString()
            }
        };

        // Act
        Func<Task> act = async () => await svc.CreateOrUpdateFSMParentUser(request);

        // Assert
        act.Should().ThrowExactlyAsync<DbUpdateException>();
    }
}