// Ignore Spelling: Levenshtein

using System.Net;
using System.Security.Claims;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class AuditGatewayTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IHttpContextAccessor _httpContextAccessor;
    private IMapper _mapper;
    private AuditGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        _httpContextAccessor = new HttpContextAccessor();
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

        _sut = new AuditGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper, _httpContextAccessor);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public void Given_validRequest_AuditAdd_Should_Return_New_Guid()
    {
        // Arrange
        var request = _fixture.Create<AuditData>();

        // Act
        var response = _sut.AuditAdd(request);

        // Assert
        response.Result.Should().BeOfType<string>();
    }

    [Test]
    public void Given_DB_Add_Should_ThrowException()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        var svc = new AuditGateway(new NullLoggerFactory(), db.Object, _mapper, _httpContextAccessor);
        db.Setup(x => x.Audits.AddAsync(It.IsAny<Audit>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());
        var request = _fixture.Create<AuditData>();

        // Act
        Func<Task> act = async () => await svc.AuditAdd(request);

        // Assert
        act.Should().ThrowExactlyAsync<Exception>();
    }

    [Test]
    public void Given_ValidRequest_AuditDataGet_Should_Return_AuditData()
    {
        // Arrange
        var type = AuditType.User; // Replace with a valid AuditType
        var id = "test-id";
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/test-path";
        httpContext.Request.Method = "GET";
        var claims = new List<Claim>
        {
            new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "test-user")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _httpContextAccessor.HttpContext = httpContext;

        // Act
        var result = _sut.AuditDataGet(type, id);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(type);
        result.typeId.Should().Be(id);
        result.url.Should().Be("localhost/test-path");
        result.method.Should().Be("GET");
        result.source.Should().Be("127.0.0.1");
        result.authentication.Should().Be("test-user");
    }

    [Test]
    public void Given_NullHttpContext_AuditDataGet_Should_Return_DefaultAuditData()
    {
        // Arrange
        var type = AuditType.Application;
        var id = "test-id";
        _httpContextAccessor.HttpContext = null;

        // Act
        var result = _sut.AuditDataGet(type, id);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(type);
        result.typeId.Should().Be(id);
        result.url.Should().Be("Unknown");
        result.method.Should().Be("Unknown");
        result.source.Should().Be("Unknown");
        result.authentication.Should().Be("Unknown");
    }

    [Test]
    public async Task CreateAuditEntry_WithValidData_ShouldReturnAuditId()
    {
        // Arrange
        var type = AuditType.User;
        var id = "test-id";
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/test-path";
        httpContext.Request.Method = "GET";
        var claims = new List<Claim>
        {
            new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "test-user")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _httpContextAccessor.HttpContext = httpContext;

        // Act
        var result = await _sut.CreateAuditEntry(type, id);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().NotBe(string.Empty);
    }

    [Test]
    public async Task CreateAuditEntry_WhenAuditAddThrowsException_ShouldReturnEmptyString()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        var svc = new AuditGateway(new NullLoggerFactory(), db.Object, _mapper, _httpContextAccessor);
        db.Setup(x => x.Audits.AddAsync(It.IsAny<Audit>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());
        // db.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var type = AuditType.User;
        var id = "test-id";

        // Act
        var result = await svc.CreateAuditEntry(type, id);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Test]
    public void Given_ExceptionOccurs_AuditDataGet_Should_ReturnNull()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Throws(new Exception("Test exception"));

        var svc = new AuditGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper, mockHttpContextAccessor.Object);
        var type = AuditType.User;
        var id = "test-id";

        // Act
        var result = svc.AuditDataGet(type, id);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task CreateAuditEntry_WhenAuditDataGetReturnsNull_ShouldReturnEmptyString()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Throws(new Exception("Test exception"));

        var svc = new AuditGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper, mockHttpContextAccessor.Object);
        var type = AuditType.User;
        var id = "test-id";

        // Act
        var result = await svc.CreateAuditEntry(type, id);

        // Assert
        result.Should().Be(string.Empty);
    }
}