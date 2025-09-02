using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class ApplicationGatewayTests : TestBase.TestBase
{
    private new Fixture _fixture = null!;
    private Mock<ILogger<ApplicationGateway>> _mockLogger = null!;
    private IConfiguration _configuration = null!;
    private EligibilityCheckContext _dbContext = null!;
    private ApplicationGateway _sut = null!;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _mockLogger = new Mock<ILogger<ApplicationGateway>>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EligibilityCheckContext(options);

        // Configure real configuration
        var configData = new Dictionary<string, string?>
        {
            { "HashCheckDays", "30" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Setup AutoMapper with the real mapping profile
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var mapper = config.CreateMapper();

        _sut = new ApplicationGateway(
            Mock.Of<ILoggerFactory>(f => f.CreateLogger(It.IsAny<string>()) == _mockLogger.Object),
            _dbContext,
            mapper,
            _configuration);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    #region DeleteApplication Tests

    [Test]
    public async Task DeleteApplication_ValidGuid_DeletesSuccessfully()
    {
        // Arrange
        var app = CreateTestApplication();
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteApplication(app.ApplicationID);

        // Assert
        result.Should().BeTrue();

        // Verify application is deleted
        var remainingApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationID == app.ApplicationID);
        remainingApp.Should().BeNull();
    }

    [Test]
    public async Task DeleteApplication_ApplicationWithStatuses_DeletesApplicationAndStatuses()
    {
        // Arrange
        var app = CreateTestApplication();
        var status = CreateTestApplicationStatus(app.ApplicationID);

        await _dbContext.Applications.AddAsync(app);
        await _dbContext.ApplicationStatuses.AddAsync(status);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteApplication(app.ApplicationID);

        // Assert
        result.Should().BeTrue();

        // Verify application and status are deleted
        var remainingApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationID == app.ApplicationID);
        var remainingStatus = await _dbContext.ApplicationStatuses.FirstOrDefaultAsync(s => s.ApplicationID == app.ApplicationID);
        remainingApp.Should().BeNull();
        remainingStatus.Should().BeNull();
    }

    [Test]
    public async Task DeleteApplication_NonExistentGuid_ReturnsFalse()
    {
        // Arrange
        var nonExistentGuid = Guid.NewGuid().ToString();

        // Act
        var result = await _sut.DeleteApplication(nonExistentGuid);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Archived Application Filtering Tests

    [Test]
    public async Task GetApplication_ArchivedApplication_ReturnsNull()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Archived;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetApplication(app.ApplicationID);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetApplication_NonArchivedApplication_ReturnsApplication()
    {
        // Arrange
        // First create the related entities that are required
        var localAuthority = new LocalAuthority
        {
            LocalAuthorityId = 1,
            LaName = "Test LA"
        };
        
        var establishment = new Establishment
        {
            EstablishmentId = 1,
            EstablishmentName = "Test School",
            LocalAuthorityId = localAuthority.LocalAuthorityId,
            Postcode = "SW1A 1AA",
            Street = "Test Street",
            Locality = "Test Locality",
            Town = "Test Town",
            County = "Test County",
            Type = "School",
            StatusOpen = true
        };

        var user = new User
        {
            UserID = "test-user-id",
            Email = "test@example.com",
            Reference = "test-reference"
        };

        await _dbContext.LocalAuthorities.AddAsync(localAuthority);
        await _dbContext.Establishments.AddAsync(establishment);
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        app.EstablishmentId = establishment.EstablishmentId;
        app.LocalAuthorityId = localAuthority.LocalAuthorityId;
        app.UserId = user.UserID;
        
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetApplication(app.ApplicationID);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public async Task GetLocalAuthorityIdForApplication_ArchivedApplication_ReturnsLocalAuthorityId()
    {
        // Arrange
        // First create the related entities that are required
        var localAuthority = new LocalAuthority
        {
            LocalAuthorityId = 2,
            LaName = "Test LA 2"
        };
        
        var establishment = new Establishment
        {
            EstablishmentId = 2,
            EstablishmentName = "Test School 2",
            LocalAuthorityId = localAuthority.LocalAuthorityId,
            Postcode = "SW1A 1AA",
            Street = "Test Street",
            Locality = "Test Locality",
            Town = "Test Town",
            County = "Test County",
            Type = "School",
            StatusOpen = true
        };

        await _dbContext.LocalAuthorities.AddAsync(localAuthority);
        await _dbContext.Establishments.AddAsync(establishment);
        await _dbContext.SaveChangesAsync();

        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Archived;
        app.EstablishmentId = establishment.EstablishmentId;
        app.LocalAuthorityId = localAuthority.LocalAuthorityId;
        
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetLocalAuthorityIdForApplication(app.ApplicationID);

        // Assert
        result.Should().Be(app.LocalAuthorityId);
    }

    [Test]
    public async Task GetLocalAuthorityIdForApplication_NonArchivedApplication_ReturnsLocalAuthorityId()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetLocalAuthorityIdForApplication(app.ApplicationID);

        // Assert
        result.Should().Be(app.LocalAuthorityId);
    }

    #endregion

    #region Helper Methods

    private Application CreateTestApplication()
    {
        return new Application
        {
            ApplicationID = Guid.NewGuid().ToString(),
            Reference = _fixture.Create<string>().PadRight(8)[..8], // Limit length
            Type = Domain.Enums.CheckEligibilityType.FreeSchoolMeals,
            Status = Domain.Enums.ApplicationStatus.Entitled,
            ParentFirstName = _fixture.Create<string>().PadRight(20)[..20],
            ParentLastName = _fixture.Create<string>().PadRight(20)[..20],
            ParentNationalInsuranceNumber = _fixture.Create<string>().PadRight(9)[..9],
            ParentDateOfBirth = DateTime.UtcNow.AddYears(-30),
            ParentNationalAsylumSeekerServiceNumber = _fixture.Create<string>().PadRight(20)[..20],
            ParentEmail = $"{_fixture.Create<string>().Replace("@", "").PadRight(10)[..10]}@example.com",
            ChildFirstName = _fixture.Create<string>().PadRight(20)[..20],
            ChildLastName = _fixture.Create<string>().PadRight(20)[..20],
            ChildDateOfBirth = DateTime.UtcNow.AddYears(-10),
            EstablishmentId = _fixture.Create<int>(),
            LocalAuthorityId = _fixture.Create<int>(),
            UserId = _fixture.Create<string>().PadRight(36)[..36],
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

    private CheckYourEligibility.API.Domain.ApplicationStatus CreateTestApplicationStatus(string applicationId)
    {
        return new CheckYourEligibility.API.Domain.ApplicationStatus
        {
            ApplicationStatusID = Guid.NewGuid().ToString(),
            ApplicationID = applicationId,
            Type = Domain.Enums.ApplicationStatus.Entitled,
            TimeStamp = DateTime.UtcNow
        };
    }

    #endregion
}
