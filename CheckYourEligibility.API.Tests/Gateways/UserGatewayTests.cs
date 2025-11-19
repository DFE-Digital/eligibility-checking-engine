// Ignore Spelling: Levenshtein

using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using String = string;

namespace CheckYourEligibility.API.Tests;

public class UserGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IMapper _mapper;
    private UsersGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

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

        _sut = new UsersGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public void Given_ExistingUser_User_Should_Return_Guid()
    {
        // Arrange
        var request = _fixture.Create<UserData>();
        var response = _sut.Create(request);
        // Act
        response = _sut.Create(request);

        // Assert
        response.Result.Should().BeOfType<String>();
    }

    [Test]
    public void Given_validRequest_User_Should_Return_New_Guid()
    {
        // Arrange
        var request = _fixture.Create<UserData>();

        // Act
        var response = _sut.Create(request);

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
        var request = _fixture.Create<UserData>();

        // Act
        Func<Task> act = async () => await svc.Create(request);

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

        var request = new UserData { Email = existingUser.Email, Reference = existingUser.Reference };

        // Act
        Func<Task> act = async () => await svc.Create(request);

        // Assert
        act.Should().ThrowExactlyAsync<DbUpdateException>();
    }
}