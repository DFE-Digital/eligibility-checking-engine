using AutoFixture;
using AutoMapper;
using Castle.Core.Logging;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace CheckYourEligibility.API.Tests;

public class WorkingFamiliesReportingGatewayTests() : TestBase.TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private Mock<ILogger<WorkingFamiliesReportingGateway>> _mockLogger = null!;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IMapper _mapper;
    private IConfiguration _configuration;
    private Mock<IAudit> _moqAudit;
    private List<WorkingFamiliesEvent> _events = null!;
    private WorkingFamiliesReportingGateway _sut;

    [SetUp]
    public async Task Setup()
    {
        _mockLogger = new Mock<ILogger<WorkingFamiliesReportingGateway>>();

        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(WorkingFamiliesReportingGatewayTests),
                InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            { "BulkEligibilityCheckLimit", "250" },
            { "QueueFsmCheckStandard", "notSet" },
            { "QueueFsmCheckBulk", "notSet" },
            { "HashCheckDays", "7" },
            { "Dwp:UseEcsforChecksWF", "false"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _mockLogger = new Mock<ILogger<WorkingFamiliesReportingGateway>>();


        _events = GetWorkingFamiliesEventsTestData();

        _sut = new WorkingFamiliesReportingGateway(_mapper, _fakeInMemoryDb, _mockLogger.Object);


    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_ClassifiesApplicationsAndReconfirmationsCorrectly()
    {
        // Arrange
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        context.WorkingFamiliesEvents.AddRange(_events);
        await context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode("TEST33344455");

        // Assert: root
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Count, Is.EqualTo(10),
            "Should return all events across all contiguous chains.");

        var returned = result.Data;

        // ---- APPLICATION ASSERTS ----
        var applications = returned
            .Where(x => x.Event == WorkingFamilyEventType.Application)
            .ToList();

        Assert.That(applications.Count, Is.EqualTo(3),
            "There should be exactly one Application per contiguous chain.");

        var applicationIds = applications
            .Select(x => x.Record.EventId)
            .ToList();

        Assert.That(applicationIds, Does.Contain("C1-A"));
        Assert.That(applicationIds, Does.Contain("C2-A"));
        Assert.That(applicationIds, Does.Contain("C3-A"));

        // ---- RECONFIRM ASSERTS ----
        var reconfirmations = returned
            .Where(x => x.Event == WorkingFamilyEventType.Reconfirm)
            .ToList();

        Assert.That(reconfirmations.Count, Is.EqualTo(7));

        var reconfirmationIds = reconfirmations
            .Select(x => x.Record.EventId)
            .ToList();

        Assert.That(reconfirmationIds, Does.Contain("C1-R1"));
        Assert.That(reconfirmationIds, Does.Contain("C1-R2"));
        Assert.That(reconfirmationIds, Does.Contain("C2-R1"));
        Assert.That(reconfirmationIds, Does.Contain("C2-R2"));
        Assert.That(reconfirmationIds, Does.Contain("C2-R3"));
        Assert.That(reconfirmationIds, Does.Contain("C3-R1"));
        Assert.That(reconfirmationIds, Does.Contain("C3-R2"));

        // ---- ORDER ASSERTS ----
        var submissionDates = returned
            .Select(x => x.Record.SubmissionDate)
            .ToList();

        var expectedOrder = submissionDates
            .OrderByDescending(x => x)
            .ToList();

        CollectionAssert.AreEqual(
            expectedOrder,
            submissionDates,
            "Events must be ordered by SubmissionDate DESC.");
    }


    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_WhenOnlyOneEvent_ReturnsApplication()
    {
        // Arrange
        var context = (EligibilityCheckContext)_fakeInMemoryDb;

        var singleEvent = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = "ONLY-A",
            EligibilityCode = "SINGLE123",
            ChildFirstName = "Test",
            ChildLastName = "Child",
            ParentFirstName = "Parent",
            ParentLastName = "Last",
            PartnerFirstName = "Partner",
            PartnerLastName = "LastPartner",
            ChildPostCode = "AB12CD",
            ChildDateOfBirth = new DateTime(2020, 1, 1),
            ParentNationalInsuranceNumber = "AB123456A",
            PartnerNationalInsuranceNumber = "CD987654B",
            ParentDateOfBirth = new DateTime(1980, 1, 1),
            PartnerDateOfBirth = new DateTime(1980, 1, 1),

            SubmissionDate = new DateTime(2024, 01, 10),
            ValidityStartDate = new DateTime(2024, 01, 15),
            ValidityEndDate = new DateTime(2024, 04, 15),
            DiscretionaryValidityStartDate = new DateTime(2024, 01, 15),
            GracePeriodEndDate = new DateTime(2024, 06, 30)
        };

        context.WorkingFamiliesEvents.Add(singleEvent);
        await context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode("SINGLE123");

        // Assert root object
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Count, Is.EqualTo(1));

        var returnedItem = result.Data.First();

        // Assert the single event is marked as Application
        Assert.That(returnedItem.Event, Is.EqualTo(WorkingFamilyEventType.Application));

        // Assert the record matches
        Assert.That(returnedItem.Record.EventId, Is.EqualTo("ONLY-A"));
    }


    [Test]
    public async Task GetAllWorkingFamiliesEventsByEligibilityCode_WhenNoEventsExist_ReturnsEmptyList()
    {
        // Arrange
        var context = (EligibilityCheckContext)_fakeInMemoryDb;

        context.WorkingFamiliesEvents.RemoveRange(context.WorkingFamiliesEvents);
        await context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllWorkingFamiliesEventsByEligibilityCode("NO_EVENTS");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data, Is.Empty,
            "When no events exist, the response should return an empty list.");
    }

    private static List<WorkingFamiliesEvent> GetWorkingFamiliesEventsTestData()
    {
        return new List<WorkingFamiliesEvent>()
        {
            // ============================
            // BLOCK 1 — DVSD ≤ 2024-06-30
            // Application + 2 reconfirmations
            // ============================
            new() {
                WorkingFamiliesEventID = "C1-A",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,01,15),
                DiscretionaryValidityStartDate = new DateTime(2024,01,01),
                ValidityStartDate = new DateTime(2024,01,01),
                ValidityEndDate = new DateTime(2024,04,01),
                GracePeriodEndDate = new DateTime(2024,06,30)
            },
            new() {
                WorkingFamiliesEventID = "C1-R1",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,02,10),
                DiscretionaryValidityStartDate = new DateTime(2024,02,01),
                ValidityStartDate = new DateTime(2024,02,01),
                ValidityEndDate = new DateTime(2024,05,01),
                GracePeriodEndDate = new DateTime(2024,06,30)
            },
            new() {
                WorkingFamiliesEventID = "C1-R2",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,04,05),
                DiscretionaryValidityStartDate = new DateTime(2024,04,01),
                ValidityStartDate = new DateTime(2024,04,01),
                ValidityEndDate = new DateTime(2024,07,01),
                GracePeriodEndDate = new DateTime(2024,06,30)
            },

            // ============================
            // BLOCK 2 — DVSD > previous GPED
            // Application + 3 reconfirmations
            // ============================
            new() {
                WorkingFamiliesEventID = "C2-A",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,07,02),
                DiscretionaryValidityStartDate = new DateTime(2024,07,01),   // > 2024-06-30 → new block
                ValidityStartDate = new DateTime(2024,07,01),
                ValidityEndDate = new DateTime(2024,10,01),
                GracePeriodEndDate = new DateTime(2024,12,31)
            },
            new() {
                WorkingFamiliesEventID = "C2-R1",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,08,05),
                DiscretionaryValidityStartDate = new DateTime(2024,08,01),
                ValidityStartDate = new DateTime(2024,08,01),
                ValidityEndDate = new DateTime(2024,11,01),
                GracePeriodEndDate = new DateTime(2024,12,31)
            },
            new() {
                WorkingFamiliesEventID = "C2-R2",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,10,01),
                DiscretionaryValidityStartDate = new DateTime(2024,10,01),
                ValidityStartDate = new DateTime(2024,10,01),
                ValidityEndDate = new DateTime(2025,01,01),
                GracePeriodEndDate = new DateTime(2024,12,31)
            },
            new() {
                WorkingFamiliesEventID = "C2-R3",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2024,12,15),
                DiscretionaryValidityStartDate = new DateTime(2024,12,01),
                ValidityStartDate = new DateTime(2024,12,01),
                ValidityEndDate = new DateTime(2025,03,01),
                GracePeriodEndDate = new DateTime(2024,12,31)
            },

            // ============================
            // BLOCK 3 — DVSD > previous GPED
            // Application + 2 reconfirmations
            // ============================
            new() {
                WorkingFamiliesEventID = "C3-A",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2025,03,15),
                DiscretionaryValidityStartDate = new DateTime(2025,03,15),
                ValidityStartDate = new DateTime(2025,03,15),
                ValidityEndDate = new DateTime(2025,06,15),
                GracePeriodEndDate = new DateTime(2025,09,30)
            },
            new() {
                WorkingFamiliesEventID = "C3-R1",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2025,05,10),
                DiscretionaryValidityStartDate = new DateTime(2025,05,10),
                ValidityStartDate = new DateTime(2025,05,10),
                ValidityEndDate = new DateTime(2025,08,10),
                GracePeriodEndDate = new DateTime(2025,09,30)
            },
            new() {
                WorkingFamiliesEventID = "C3-R2",
                EligibilityCode = "TEST33344455",
                ChildFirstName = "Test",
                ChildLastName = "Child",
                ParentFirstName = "Parent",
                ParentLastName = "Last",
                PartnerFirstName = "Partner",
                PartnerLastName = "LPartner",
                ChildPostCode = "AB12CD",
                ChildDateOfBirth = new DateTime(2020,1,1),
                ParentNationalInsuranceNumber = "AB123456A",
                PartnerNationalInsuranceNumber = "CD987654B",
                ParentDateOfBirth = new DateTime(1980, 1, 1),
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                SubmissionDate = new DateTime(2025,08,20),
                DiscretionaryValidityStartDate = new DateTime(2025,08,15),
                ValidityStartDate = new DateTime(2025,08,15),
                ValidityEndDate = new DateTime(2025,11,15),
                GracePeriodEndDate = new DateTime(2025,09,30)
            }
        };
    }
}