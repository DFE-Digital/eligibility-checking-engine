using AutoFixture;
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

        _sut = new ApplicationGateway(
            Mock.Of<ILoggerFactory>(f => f.CreateLogger(It.IsAny<string>()) == _mockLogger.Object),
            _dbContext,
            Mock.Of<AutoMapper.IMapper>(),
            _configuration);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    #region BulkDeleteApplications Tests

    [Test]
    public async Task BulkDeleteApplications_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var guids = new List<string>();

        // Act
        var result = await _sut.BulkDeleteApplications(guids);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task BulkDeleteApplications_ValidApplications_DeletesSuccessfully()
    {
        // Arrange
        var app1 = CreateTestApplication();
        var app2 = CreateTestApplication();
        
        await _dbContext.Applications.AddRangeAsync(app1, app2);
        await _dbContext.SaveChangesAsync();

        var guids = new List<string> { app1.ApplicationID, app2.ApplicationID };

        // Act
        var result = await _sut.BulkDeleteApplications(guids);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[app1.ApplicationID].Should().BeTrue();
        result[app2.ApplicationID].Should().BeTrue();

        // Verify applications are deleted from database
        var remainingApps = await _dbContext.Applications.ToListAsync();
        remainingApps.Should().BeEmpty();
    }

    [Test]
    public async Task BulkDeleteApplications_ApplicationsWithStatuses_DeletesApplicationsAndStatuses()
    {
        // Arrange
        var app = CreateTestApplication();
        var status1 = CreateTestApplicationStatus(app.ApplicationID);
        var status2 = CreateTestApplicationStatus(app.ApplicationID);

        await _dbContext.Applications.AddAsync(app);
        await _dbContext.ApplicationStatuses.AddRangeAsync(status1, status2);
        await _dbContext.SaveChangesAsync();

        var guids = new List<string> { app.ApplicationID };

        // Act
        var result = await _sut.BulkDeleteApplications(guids);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[app.ApplicationID].Should().BeTrue();

        // Verify application and statuses are deleted
        var remainingApps = await _dbContext.Applications.ToListAsync();
        var remainingStatuses = await _dbContext.ApplicationStatuses.ToListAsync();
        remainingApps.Should().BeEmpty();
        remainingStatuses.Should().BeEmpty();
    }

    [Test]
    public async Task BulkDeleteApplications_NonExistentApplications_ReturnsFalse()
    {
        // Arrange
        var nonExistentGuid1 = Guid.NewGuid().ToString();
        var nonExistentGuid2 = Guid.NewGuid().ToString();
        var guids = new List<string> { nonExistentGuid1, nonExistentGuid2 };

        // Act
        var result = await _sut.BulkDeleteApplications(guids);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[nonExistentGuid1].Should().BeFalse();
        result[nonExistentGuid2].Should().BeFalse();
    }

    [Test]
    public async Task BulkDeleteApplications_MixedExistentAndNonExistent_ReturnsCorrectResults()
    {
        // Arrange
        var existentApp = CreateTestApplication();
        await _dbContext.Applications.AddAsync(existentApp);
        await _dbContext.SaveChangesAsync();

        var nonExistentGuid = Guid.NewGuid().ToString();
        var guids = new List<string> { existentApp.ApplicationID, nonExistentGuid };

        // Act
        var result = await _sut.BulkDeleteApplications(guids);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[existentApp.ApplicationID].Should().BeTrue();
        result[nonExistentGuid].Should().BeFalse();

        // Verify only the existent application was deleted
        var remainingApps = await _dbContext.Applications.ToListAsync();
        remainingApps.Should().BeEmpty();
    }

    #endregion

    #region GetLocalAuthorityIdsForApplications Tests

    [Test]
    public async Task GetLocalAuthorityIdsForApplications_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var applicationIds = new List<string>();

        // Act
        var result = await _sut.GetLocalAuthorityIdsForApplications(applicationIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetLocalAuthorityIdsForApplications_ValidApplications_ReturnsCorrectMapping()
    {
        // Arrange
        var app1 = CreateTestApplication();
        app1.LocalAuthorityId = 1;
        
        var app2 = CreateTestApplication();
        app2.LocalAuthorityId = 2;

        await _dbContext.Applications.AddRangeAsync(app1, app2);
        await _dbContext.SaveChangesAsync();

        var applicationIds = new List<string> { app1.ApplicationID, app2.ApplicationID };

        // Act
        var result = await _sut.GetLocalAuthorityIdsForApplications(applicationIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[app1.ApplicationID].Should().Be(1);
        result[app2.ApplicationID].Should().Be(2);
    }

    [Test]
    public async Task GetLocalAuthorityIdsForApplications_NonExistentApplications_ReturnsEmptyDictionary()
    {
        // Arrange
        var nonExistentId1 = Guid.NewGuid().ToString();
        var nonExistentId2 = Guid.NewGuid().ToString();
        var applicationIds = new List<string> { nonExistentId1, nonExistentId2 };

        // Act
        var result = await _sut.GetLocalAuthorityIdsForApplications(applicationIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetLocalAuthorityIdsForApplications_MixedExistentAndNonExistent_ReturnsOnlyExistent()
    {
        // Arrange
        var existentApp = CreateTestApplication();
        existentApp.LocalAuthorityId = 5;
        await _dbContext.Applications.AddAsync(existentApp);
        await _dbContext.SaveChangesAsync();

        var nonExistentId = Guid.NewGuid().ToString();
        var applicationIds = new List<string> { existentApp.ApplicationID, nonExistentId };

        // Act
        var result = await _sut.GetLocalAuthorityIdsForApplications(applicationIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[existentApp.ApplicationID].Should().Be(5);
        result.ContainsKey(nonExistentId).Should().BeFalse();
    }

    #endregion

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
