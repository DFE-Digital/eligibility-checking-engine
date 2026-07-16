using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ApplicationStatus = CheckYourEligibility.API.Domain.ApplicationStatus;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class ApplicationGatewayTests : TestBase.TestBase
{
    private Mock<ILogger<ApplicationGateway>> _mockLogger = null!;
    private IConfiguration _configuration = null!;
    private EligibilityCheckContext _dbContext = null!;
    private ApplicationGateway _sut = null!;
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ApplicationGateway>>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(ApplicationGatewayTests), InMemoryDatabaseRoot)
            .Options;

        _dbContext = new EligibilityCheckContext(options);

        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();

        // Configure real configuration
        var configData = new Dictionary<string, string?>
        {
            { "HashCheckDays", "30" },
            { "HashCheckDaysWF", "1" }
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
            LocalAuthorityID = 1,
            LaName = "Test LA"
        };

        var establishment = new Domain.Establishment
        {
            EstablishmentID = 1,
            EstablishmentName = "Test School",
            LocalAuthorityID = localAuthority.LocalAuthorityID,
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
            Reference = "test-reference",
            UserType = UserType.FreeSchoolMealsParent
        };

        await _dbContext.LocalAuthorities.AddAsync(localAuthority);
        await _dbContext.Establishments.AddAsync(establishment);
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        app.EstablishmentId = establishment.EstablishmentID;
        app.LocalAuthorityID = localAuthority.LocalAuthorityID;
        app.UserId = user.UserID;

        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetApplication(app.ApplicationID);

        // Assert
        result.Should().NotBeNull();
    }

    [Test]
    public async Task GetLocalAuthorityIDForApplication_ArchivedApplication_ReturnsLocalAuthorityID()
    {
        // Arrange
        // First create the related entities that are required
        var localAuthority = new LocalAuthority
        {
            LocalAuthorityID = 2,
            LaName = "Test LA 2"
        };

        var establishment = new Domain.Establishment
        {
            EstablishmentID = 2,
            EstablishmentName = "Test School 2",
            LocalAuthorityID = localAuthority.LocalAuthorityID,
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
        app.EstablishmentId = establishment.EstablishmentID;
        app.LocalAuthorityID = localAuthority.LocalAuthorityID;

        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetLocalAuthorityIdForApplication(app.ApplicationID);

        // Assert
        result.Should().Be(app.LocalAuthorityID);
    }

    [Test]
    public async Task GetLocalAuthorityIDForApplication_NonArchivedApplication_ReturnsLocalAuthorityID()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetLocalAuthorityIdForApplication(app.ApplicationID);

        // Assert
        result.Should().Be(app.LocalAuthorityID);
    }

    #endregion

    #region RestoreArchivedApplicationStatus Tests

    [Test]
    public async Task RestoreArchivedApplicationStatus_ShouldRestorePreviousStatus_WhenApplicationIsArchived()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Archived;
        await _dbContext.Applications.AddAsync(app);
        var previousStatus = CreateTestApplicationStatus(app.ApplicationID);
        previousStatus.Type = Domain.Enums.ApplicationStatus.Entitled;
        await _dbContext.ApplicationStatuses.AddAsync(previousStatus);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.RestoreArchivedApplicationStatus(app.ApplicationID);

        // Assert
        result.Should().BeOfType<ApplicationStatusRestoreResponse>();
        var updatedApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationID == app.ApplicationID);
        updatedApp.Should().NotBeNull();
        updatedApp!.Status.Should().Be(Domain.Enums.ApplicationStatus.Entitled);
    }

    [Test]
    public async Task RestoreArchivedApplicationStatus_ShouldRestorePreviousTier_WhenApplicationIsArchived()
    {
        // Arrange - application was 'expanded' at the time it was archived (per CreateTestApplication default),
        // but the last non-archived history entry recorded 'targeted' - restore should bring back 'targeted'.
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Archived;
        app.Tier = EligibilityTier.expanded;
        await _dbContext.Applications.AddAsync(app);
        var previousStatus = CreateTestApplicationStatus(app.ApplicationID);
        previousStatus.Type = Domain.Enums.ApplicationStatus.Entitled;
        previousStatus.Tier = EligibilityTier.targeted;
        await _dbContext.ApplicationStatuses.AddAsync(previousStatus);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.RestoreArchivedApplicationStatus(app.ApplicationID);

        // Assert
        result.Data.Tier.Should().Be(EligibilityTier.targeted.ToString());

        var updatedApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationID == app.ApplicationID);
        updatedApp.Should().NotBeNull();
        updatedApp!.Tier.Should().Be(EligibilityTier.targeted);

        var newHistoryEntry = await _dbContext.ApplicationStatuses
            .Where(s => s.ApplicationID == app.ApplicationID)
            .OrderByDescending(s => s.TimeStamp)
            .FirstOrDefaultAsync();
        newHistoryEntry.Should().NotBeNull();
        newHistoryEntry!.Tier.Should().Be(EligibilityTier.targeted);
    }

    [Test]
    public async Task RestoredArchivedApplicationStatus_ShouldThrowNotFoundException_WhenApplicationDoesNotExist()
    {
        // Arrange
        var nonExistentGuid = Guid.NewGuid().ToString();

        // Act
        Func<Task> act = async () => await _sut.RestoreArchivedApplicationStatus(nonExistentGuid);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task RestoreArchivedApplicationStatus_ShouldThrow_BadRequest_WhenApplicationIsNotArchived()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();
        // Act
        Func<Task> act = async () => await _sut.RestoreArchivedApplicationStatus(app.ApplicationID);
        // Assert
        await act.Should().ThrowAsync<BadRequest>();
    }

    [Test]
    public async Task RestoreArchivedApplicationStatus_ShouldThrow_BadRequest_WhenNoNonArchivedHistoryExists()
    {
        // Arrange - simulates a legacy bulk-imported application with no status history at all
        // (or one whose only history entry is Archived itself)
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Archived;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _sut.RestoreArchivedApplicationStatus(app.ApplicationID);

        // Assert
        await act.Should().ThrowAsync<BadRequest>()
            .WithMessage("No previous non-archived status found for this application, unable to restore");
    }

    #endregion

    #region BulkImportApplications Tests

    [Test]
    public async Task BulkImportApplications_ShouldCreateStatusHistory_ForEachApplication()
    {
        // Arrange
        var app1 = CreateTestApplication();
        app1.Status = Domain.Enums.ApplicationStatus.Receiving;
        var app2 = CreateTestApplication();
        app2.Status = Domain.Enums.ApplicationStatus.Entitled;
        var applications = new List<Application> { app1, app2 };

        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        List<Application> capturedApplications = null!;
        List<ApplicationStatus> capturedStatusHistory = null!;
        db.Setup(x => x.BulkInsert_Applications(It.IsAny<IEnumerable<Application>>(), It.IsAny<IEnumerable<ApplicationStatus>>()))
            .Callback<IEnumerable<Application>, IEnumerable<ApplicationStatus>>((apps, statuses) =>
            {
                capturedApplications = apps.ToList();
                capturedStatusHistory = statuses.ToList();
            });

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var mapper = config.CreateMapper();
        var sut = new ApplicationGateway(
            Mock.Of<ILoggerFactory>(f => f.CreateLogger(It.IsAny<string>()) == _mockLogger.Object),
            db.Object,
            mapper,
            _configuration);

        // Act
        await sut.BulkImportApplications(applications);

        // Assert
        capturedApplications.Should().HaveCount(2);
        capturedStatusHistory.Should().HaveCount(2);
        capturedStatusHistory.Should()
            .ContainSingle(s => s.ApplicationID == app1.ApplicationID && s.Type == Domain.Enums.ApplicationStatus.Receiving);
        capturedStatusHistory.Should()
            .ContainSingle(s => s.ApplicationID == app2.ApplicationID && s.Type == Domain.Enums.ApplicationStatus.Entitled);
        capturedStatusHistory.Should().OnlyContain(s => !string.IsNullOrEmpty(s.ApplicationStatusID));
        capturedStatusHistory.Should().OnlyContain(s => s.Tier == EligibilityTier.expanded);
    }

    [Test]
    public async Task BulkImportApplications_NoApplications_DoesNotCallBulkInsert()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var mapper = config.CreateMapper();
        var sut = new ApplicationGateway(
            Mock.Of<ILoggerFactory>(f => f.CreateLogger(It.IsAny<string>()) == _mockLogger.Object),
            db.Object,
            mapper,
            _configuration);

        // Act
        await sut.BulkImportApplications(new List<Application>());

        // Assert
        db.Verify(
            x => x.BulkInsert_Applications(It.IsAny<IEnumerable<Application>>(), It.IsAny<IEnumerable<ApplicationStatus>>()),
            Times.Never);
    }

    #endregion

    #region UpdateApplication Tests

    [Test]
    public async Task UpdateApplication_TierOnlyChange_CreatesStatusHistoryEntry()
    {
        // Arrange - Tier changes without a Status change should still be tracked in history
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.Entitled;
        app.Tier = EligibilityTier.targeted;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        var updateData = new Boundary.Requests.ApplicationUpdateData { Tier = EligibilityTier.expanded };

        // Act
        await _sut.UpdateApplication(app.ApplicationID, updateData);

        // Assert
        var updatedApp = await _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationID == app.ApplicationID);
        updatedApp!.Tier.Should().Be(EligibilityTier.expanded);

        var historyEntries = await _dbContext.ApplicationStatuses
            .Where(s => s.ApplicationID == app.ApplicationID)
            .ToListAsync();
        historyEntries.Should().ContainSingle();
        historyEntries[0].Type.Should().Be(Domain.Enums.ApplicationStatus.Entitled);
        historyEntries[0].Tier.Should().Be(EligibilityTier.expanded);
    }

    [Test]
    public async Task UpdateApplication_StatusAndTierChangedTogether_CreatesOnlyOneStatusHistoryEntry()
    {
        // Arrange
        var app = CreateTestApplication();
        app.Status = Domain.Enums.ApplicationStatus.SentForReview;
        app.Tier = null;
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        var updateData = new Boundary.Requests.ApplicationUpdateData
        {
            Status = Domain.Enums.ApplicationStatus.Entitled,
            Tier = EligibilityTier.expanded
        };

        // Act
        await _sut.UpdateApplication(app.ApplicationID, updateData);

        // Assert
        var historyEntries = await _dbContext.ApplicationStatuses
            .Where(s => s.ApplicationID == app.ApplicationID)
            .ToListAsync();
        historyEntries.Should().ContainSingle();
        historyEntries[0].Type.Should().Be(Domain.Enums.ApplicationStatus.Entitled);
        historyEntries[0].Tier.Should().Be(EligibilityTier.expanded);
    }

    [Test]
    public async Task UpdateApplication_NoStatusOrTierChange_DoesNotCreateStatusHistoryEntry()
    {
        // Arrange - e.g. only the establishment is being changed
        var app = CreateTestApplication();
        var establishment = new Domain.Establishment
        {
            EstablishmentID = _fixture.Create<int>(),
            EstablishmentName = "Another School",
            LocalAuthorityID = app.LocalAuthorityID,
            Postcode = "AB1 2CD",
            Street = "Street",
            Locality = "",
            Town = "Town",
            County = "County",
            Type = "School",
            StatusOpen = true
        };
        await _dbContext.Establishments.AddAsync(establishment);
        await _dbContext.Applications.AddAsync(app);
        await _dbContext.SaveChangesAsync();

        var updateData = new Boundary.Requests.ApplicationUpdateData { EstablishmentUrn = establishment.EstablishmentID };

        // Act
        await _sut.UpdateApplication(app.ApplicationID, updateData);

        // Assert
        var historyEntries = await _dbContext.ApplicationStatuses
            .Where(s => s.ApplicationID == app.ApplicationID)
            .ToListAsync();
        historyEntries.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private Application CreateTestApplication()
    {
        return new Application
        {
            ApplicationID = Guid.NewGuid().ToString(),
            Reference = _fixture.Create<string>().PadRight(8)[..8], // Limit length
            Type = CheckEligibilityType.FreeSchoolMeals,
            Status = Domain.Enums.ApplicationStatus.Entitled,
            Tier =  EligibilityTier.expanded,
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
            LocalAuthorityID = _fixture.Create<int>(),
            UserId = _fixture.Create<string>().PadRight(36)[..36],
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

    private ApplicationStatus CreateTestApplicationStatus(string applicationId)
    {
        return new ApplicationStatus
        {
            ApplicationStatusID = Guid.NewGuid().ToString(),
            ApplicationID = applicationId,
            Type = Domain.Enums.ApplicationStatus.Entitled,
            TimeStamp = DateTime.UtcNow
        };
    }

    #endregion
}