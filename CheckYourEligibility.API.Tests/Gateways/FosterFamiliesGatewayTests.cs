using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.Tests.Gateways;

public class FosterFamiliesGatewayTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private Mock<IWorkingFamiliesEvent> _mockWFEventGateway = null!;
    private FosterFamiliesGateway _sut;
    private Mock<ILogger<FosterFamiliesGateway>> _mockLogger = null!;
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public async Task SetUpAsync()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(EligibilityCheckReportingGatewayTests), InMemoryDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _mockWFEventGateway = new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<FosterFamiliesGateway>>();

        _mockWFEventGateway
            .Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(It.IsAny<string>()))
            .ReturnsAsync((string eligibilityCode) =>
                _fakeInMemoryDb.WorkingFamiliesEventSummaries.FirstOrDefault(x => x.EligibilityCode == eligibilityCode));

        _mockWFEventGateway
            .Setup(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(It.IsAny<string>()))
            .ReturnsAsync((string eligibilityCode) =>
                _fakeInMemoryDb.WorkingFamiliesEvents
                    .Where(x => x.EligibilityCode == eligibilityCode && !x.IsDeleted)
                    .OrderByDescending(x => x.SubmissionDate)
                    .FirstOrDefault());

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        await context.EligibilityCodeRanges.AddAsync(new EligibilityCodeRange
        {
            EligibilityCodeRangeId = 1,
            Name = EligibilityCodeType.Foster,
            StartRange = 40000000001,
            EndRange = 49999999999,
            NextAvailableCode = 40000000001
        });

        await context.SaveChangesAsync();

        _sut = new FosterFamiliesGateway(_fakeInMemoryDb, _mockWFEventGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    #region  Get Foster Family

    [Test]
    public async Task GetFosterFamily_Should_Include_Children_When_Requested()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        var fosterCarer = new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            LocalAuthorityID = 0,
            FirstName = "John",
            LastName = "Smith",
            NationalInsuranceNumber = "NN123456C",
        };

        fosterCarer.FosterChildren.Add(new FosterChild
        {
            FosterChildId = Guid.NewGuid(),
            FirstName = "Child",
            LastName = "One",
            EligibilityCode = "ELIG001",
            PostCode = "NAU 1EE",
            Status = "Active"
        });

        _fakeInMemoryDb.FosterCarers.Add(fosterCarer);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetFosterFamily(fosterCarerId, 0, true);

        // Assert
        result.Should().NotBeNull();
        result.FosterChildren.Should().HaveCount(1);

        var child = result.FosterChildren.Single();

        child.FirstName.Should().Be("Child");
        child.LastName.Should().Be("One");
        child.EligibilityCode.Should().Be("ELIG001");
    }

    [Test]
    public async Task GetFosterFamily_Should_Not_Include_Children_When_Not_Requested()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        var fosterCarer = new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            FirstName = "John",
            LastName = "Smith",
            NationalInsuranceNumber = "NN123456C",
            LocalAuthorityID = 0
        };

        _fakeInMemoryDb.FosterCarers.Add(fosterCarer);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetFosterFamily(fosterCarerId, 0, false);

        // Assert
        result.FosterChildren.Should().BeEmpty();
    }

    [Test]
    public async Task GetFosterFamily_Should_Return_Not_Found_Exception()
    {
        // Act
        Func<Task> act = async () => await _sut.GetFosterFamily(Guid.NewGuid(), 0, true);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Create Foster Family

    [Test]
    public async Task CreateFosterFamily_Should_Not_Log_Database_Error_Details()
    {
        const string privateValue = "PRIVATE-FOSTER-GATEWAY-3644";
        var sourceException = new DbUpdateException(
            $"Database error containing {privateValue}",
            new InvalidOperationException($"Inner error containing {privateValue}"));

        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new FailWorkingFamiliesEventSaveInterceptor(sourceException))
            .Options;

        await using var context = new EligibilityCheckContext(options);

        context.EligibilityCodeRanges.Add(new EligibilityCodeRange
        {
            EligibilityCodeRangeId = 1,
            Name = EligibilityCodeType.Foster,
            StartRange = 40000000001,
            EndRange = 49999999999,
            NextAvailableCode = 40000000001
        });
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<FosterFamiliesGateway>>();
        var gateway = new FosterFamiliesGateway(context, _mockWFEventGateway.Object, logger.Object);

        Func<Task> act = () => gateway.CreateFosterFamily(BuildValidRequest(DateTime.UtcNow));

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.Should().BeSameAs(sourceException);

        var logCalls = logger.Invocations
            .Where(invocation => invocation.Method.Name == "Log")
            .ToList();

        logCalls.Should().ContainSingle(
            invocation => (LogLevel)invocation.Arguments[0] == LogLevel.Error);

        foreach (var logCall in logCalls)
        {
            // ILogger.Log argument 3 holds the exception object.
            logCall.Arguments[3].Should().BeNull(
                "the original exception may contain submitted personal data");

            var state = (IEnumerable<KeyValuePair<string, object?>>)
                logCall.Arguments[2];

            foreach (var entry in state)
            {
                (entry.Value?.ToString() ?? string.Empty)
                    .Should().NotContain(privateValue);
            }

            (logCall.Arguments[2].ToString() ?? string.Empty)
                .Should().NotContain(privateValue);
        }
    }

    [Test]
    public async Task CreateFosterFamily_Should_Return_Created_Response()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        var result = await _sut.CreateFosterFamily(request);

        // Assert
        result.Should().NotBeNull();
        result.FosterChildId.Should().NotBeEmpty();
        result.FosterCarerId.Should().NotBeEmpty();
    }

    [Test]
    public async Task CreateFosterFamily_Should_Return_Created_Response_But_Different_LA_same_NINO()
    {
        // Arrange
        // LA is 0 
        var request1 = BuildValidRequest(DateTime.UtcNow);
        string request1NINO = request1.FosterCarer.CarerNationalInsuranceNumber;
        await _sut.CreateFosterFamily(request1);

        // Act
        // LA is now 123 but NINO is same
        var request2 = BuildValidRequest(DateTime.UtcNow);
        request2.FosterCarer.LocalAuthorityID = 123;
        request2.FosterCarer.CarerNationalInsuranceNumber = request1NINO;
        var result = await _sut.CreateFosterFamily(request2);

        // Assert
        result.Should().NotBeNull();
        result.FosterChildId.Should().NotBeEmpty();
        result.FosterCarerId.Should().NotBeEmpty();
    }

    [Test]
    public async Task CreateFosterFamily_Should_Link_Child_To_FosterCarer()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        var fosterCarer = await _fakeInMemoryDb.FosterCarers.SingleAsync();
        var fosterChild = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        fosterChild.FosterCarerId.Should().Be(fosterCarer.FosterCarerId);
    }

    [Test]
    public async Task CreateFosterFamily_Should_Create_WorkingFamilies_Event()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        _fakeInMemoryDb.WorkingFamiliesEvents.Should().HaveCount(1);
    }

    [Test]
    public async Task CreateFosterFamily_Should_Set_EligibilityCode_On_Child()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        var response = await _sut.CreateFosterFamily(request);

        // Assert
        var fosterChild = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        fosterChild.EligibilityCode.Should().NotBeEmpty();
    }

    [Test]
    [Obsolete]
    public async Task CreateFosterFamily_Should_Set_Validity_Dates()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        var fosterChild = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        fosterChild.ValidityStartDate.Should().NotBe(default);
        fosterChild.ValidityEndDate.Should().NotBe(default);
        fosterChild.ValidityEndDate.Should().BeAfter(fosterChild.ValidityStartDate);
    }

    [Test]
    public async Task CreateFosterFamily_Should_CreateLinkedSummary()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        var fosterChild = await _fakeInMemoryDb.FosterChildren.Include(x => x.WorkingFamiliesEventSummary).SingleAsync();

        fosterChild.WorkingFamiliesEventSummary.Should().NotBeNull();
        fosterChild.WorkingFamiliesEventSummary.ValidityStartDate.Should().NotBe(default);
        fosterChild.WorkingFamiliesEventSummary.ValidityEndDate.Should().NotBe(default);
        fosterChild.WorkingFamiliesEventSummary.ValidityEndDate.Should().BeAfter(fosterChild.ValidityStartDate);
    }

    [Test]
    public async Task CreateFosterFamily_Should_Throw_When_Request_Is_Null()
    {
        // Act
        Func<Task> act = () => _sut.CreateFosterFamily(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task CreateFosterFamily_Should_Throw_ValidationException_When_Carer_Already_Exists()
    {
        // Arrange
        // When the LA already contains a family with SAME nino
        var request = BuildValidRequest(DateTime.UtcNow);
        request.FosterCarer.CarerNationalInsuranceNumber = "AA123456A";

        await _sut.CreateFosterFamily(request);

        Func<Task> act = () => _sut.CreateFosterFamily(request);

        var exception = await act.Should()
            .ThrowAsync<ValidationException>();

        exception.Which.Message.Should().Be(
            "A foster family with this National Insurance number already exists.");

        exception.Which.ToString().Should()
            .NotContain(request.FosterCarer.CarerNationalInsuranceNumber);

        (await _fakeInMemoryDb.FosterCarers.CountAsync()).Should().Be(1);
        (await _fakeInMemoryDb.FosterChildren.CountAsync()).Should().Be(1);
        (await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync()).Should().Be(1);
    }

    #endregion

    #region Update Foster Family

    [Test]
    public async Task UpdateFosterCarer_Should_Update_Carer_Details()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            FirstName = "John",
            LastName = "Smith",
            LocalAuthorityID = 0,
            DateOfBirth = new DateTime(1980, 1, 1),
            NationalInsuranceNumber = "AA123456A"
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = new DateTime(1985, 1, 1),
                CarerNationalInsuranceNumber = "BB123456B"
            }
        };

        // Act
        await _sut.UpdateFosterCarer(fosterCarerId, 0, request);

        // Assert
        var updated = await _fakeInMemoryDb.FosterCarers
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        updated.FirstName.Should().Be("Peter");
        updated.LastName.Should().Be("Jones");
        updated.DateOfBirth.Should().Be(new DateTime(1985, 1, 1));
        updated.NationalInsuranceNumber.Should().Be("BB123456B");
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Update_Partner_Details()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            FirstName = "John",
            LastName = "Smith",
            LocalAuthorityID = 0,
            NationalInsuranceNumber = "BB123456B"
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new UpdateFosterCarerRequest
        {
            FosterPartnerRequest = new FosterPartnerRequest
            {
                PartnerFirstName = "Jane",
                PartnerLastName = "Smith",
                PartnerDateOfBirth = new DateTime(1982, 1, 1),
                PartnerNationalInsuranceNumber = "CC123456C"
            }
        };

        // Act
        await _sut.UpdateFosterCarer(fosterCarerId, 0, request);

        // Assert
        var updated = await _fakeInMemoryDb.FosterCarers
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        updated.HasPartner.Should().BeTrue();
        updated.PartnerFirstName.Should().Be("Jane");
        updated.PartnerLastName.Should().Be("Smith");
        updated.PartnerNationalInsuranceNumber.Should().Be("CC123456C");
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Update_Carer_And_Partner_Details()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            FirstName = "John",
            LastName = "Smith",
            LocalAuthorityID = 0,
            NationalInsuranceNumber = "BB123456B"
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = new DateTime(1985, 1, 1),
                CarerNationalInsuranceNumber = "BB123456B"
            },
            FosterPartnerRequest = new FosterPartnerRequest
            {
                PartnerFirstName = "Sarah",
                PartnerLastName = "Jones",
                PartnerDateOfBirth = new DateTime(1986, 1, 1),
                PartnerNationalInsuranceNumber = "DD123456D"
            }
        };

        // Act
        await _sut.UpdateFosterCarer(fosterCarerId, 0, request);

        // Assert
        var updated = await _fakeInMemoryDb.FosterCarers
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        updated.FirstName.Should().Be("Peter");
        updated.PartnerFirstName.Should().Be("Sarah");
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Update_Working_Families_Summary_And_Latest_Event()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var fosterChild = await _fakeInMemoryDb.FosterChildren
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        var summary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        var latestEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        _mockWFEventGateway
            .Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(summary);

        _mockWFEventGateway
            .Setup(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(latestEvent);

        var updateRequest = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = new DateTime(1985, 1, 1),
                CarerNationalInsuranceNumber = "ZZ123456Z"
            },
            FosterPartnerRequest = new FosterPartnerRequest
            {
                PartnerFirstName = "Sarah",
                PartnerLastName = "Jones",
                PartnerDateOfBirth = new DateTime(1986, 1, 1),
                PartnerNationalInsuranceNumber = "DD123456D"
            }
        };

        // Act
        await _sut.UpdateFosterCarer(fosterCarerId, 0, updateRequest);

        // Assert
        var updatedSummary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedSummary.ParentLastName.Should().Be("Jones");
        updatedSummary.ParentNationalInsuranceNumber.Should().Be("ZZ123456Z");
        updatedSummary.PartnerLastName.Should().Be("Jones");
        updatedSummary.PartnerNationalInsuranceNumber.Should().Be("DD123456D");
        updatedSummary.LastUpdatedDate.Should().NotBe(default);

        var updatedEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedEvent.ParentLastName.Should().Be("Jones");
        updatedEvent.ParentNationalInsuranceNumber.Should().Be("ZZ123456Z");
        updatedEvent.PartnerLastName.Should().Be("Jones");
        updatedEvent.PartnerNationalInsuranceNumber.Should().Be("DD123456D");

        _mockWFEventGateway.Verify(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
        _mockWFEventGateway.Verify(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Not_Throw_When_Working_Family_Summary_Or_Event_Is_Missing()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var fosterChild = await _fakeInMemoryDb.FosterChildren
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        _mockWFEventGateway
            .Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync((WorkingFamiliesEventSummary?)null);

        _mockWFEventGateway
            .Setup(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync((WorkingFamiliesEvent?)null);

        var updateRequest = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = new DateTime(1985, 1, 1),
                CarerNationalInsuranceNumber = "ZZ123456Z"
            }
        };

        // Act
        Func<Task> act = async () => await _sut.UpdateFosterCarer(fosterCarerId, 0, updateRequest);

        // Assert
        await act.Should().NotThrowAsync();

        var updatedCarer = await _fakeInMemoryDb.FosterCarers
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        updatedCarer.FirstName.Should().Be("Peter");
        updatedCarer.LastName.Should().Be("Jones");
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Throw_NotFoundException_When_Carer_Does_Not_Exist()
    {
        // Arrange
        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = DateTime.Today,
                CarerNationalInsuranceNumber = "BB123456B"
            }
        };

        // Act
        Func<Task> act = () =>
            _sut.UpdateFosterCarer(Guid.NewGuid(), 0, request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task UpdateFosterCarer_Should_Throw_NotFoundException_When_LA_Does_Not_Match()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            FirstName = "John",
            LastName = "Smith",
            LocalAuthorityID = 0,
            NationalInsuranceNumber = "BB123456B"
        });

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = "Peter",
                CarerLastName = "Jones",
                CarerDateOfBirth = DateTime.Today,
                CarerNationalInsuranceNumber = "BB123456B"
            }
        };

        // Act
        Func<Task> act = () =>
            _sut.UpdateFosterCarer(
                fosterCarerId,
                123, // wrong LA Id
                request
            );


        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster carer {fosterCarerId} not found");

    }

    #endregion

    #region Delete Foster Carer OR Foster Carer's Partner

    [Test]
    public async Task DeleteFosterCarer_Should_Delete_FosterCarer()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        // Act
        await _sut.DeleteFosterCarer(fosterCarerId, 0);

        // Assert
        _fakeInMemoryDb.FosterCarers.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteFosterCarer_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        // Act
        Func<Task> act = () =>
            _sut.DeleteFosterCarer(
            fosterCarerId,
            123); // wrong LA 


        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster carer {fosterCarerId} not found");
    }

    [Test]
    public async Task DeleteFosterPartner_Should_Remove_Partner_Details_And_Update_Working_Families_Summary_And_Latest_Event()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var fosterChild = await _fakeInMemoryDb.FosterChildren
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        var summary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        var latestEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        _mockWFEventGateway
            .Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(summary);

        _mockWFEventGateway
            .Setup(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(latestEvent);

        // Act
        await _sut.DeleteFosterPartner(fosterCarerId, 0);

        // Assert
        var fosterCarer = await _fakeInMemoryDb.FosterCarers.SingleAsync();

        fosterCarer.HasPartner.Should().BeFalse();
        fosterCarer.PartnerFirstName.Should().BeNull();
        fosterCarer.PartnerLastName.Should().BeNull();
        fosterCarer.PartnerDateOfBirth.Should().BeNull();
        fosterCarer.PartnerNationalInsuranceNumber.Should().BeNull();

        var updatedSummary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedSummary.PartnerLastName.Should().BeNull();
        updatedSummary.PartnerNationalInsuranceNumber.Should().BeNull();
        updatedSummary.LastUpdatedDate.Should().NotBe(default);

        var updatedEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedEvent.PartnerLastName.Should().BeNull();
        updatedEvent.PartnerNationalInsuranceNumber.Should().BeNull();

        _mockWFEventGateway.Verify(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
        _mockWFEventGateway.Verify(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
    }

    [Test]
    public async Task DeleteFosterPartner_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        // Act
        Func<Task> act = () =>
            _sut.DeleteFosterPartner(
                fosterCarerId,
                123 // wrong LA Id
            );


        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster carer {fosterCarerId} not found");
    }

    #endregion

    #region Search Foster Families

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Results()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().HaveCount(1);

        var item = result.Data.Single();

        item.ChildName.Should().Be("Tom Smith");
        item.CarerName.Should().Be("John Smith");
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Order_By_Reconfirmation_Status_Then_Descending_Validity_End_Date()
    {
        // Arrange
        var checkDate = new DateTime(2025, 6, 15);
        var searchRecords = new[]
        {
            (EligibilityCode: "40000000004", ValidityEndDate: new DateTime(2025, 5, 1), ChildDateOfBirth: new DateTime(2010, 1, 1)),
            (EligibilityCode: "40000000014", ValidityEndDate: new DateTime(2025, 6, 10), ChildDateOfBirth: new DateTime(2022, 1, 1)),
            (EligibilityCode: "40000000024", ValidityEndDate: new DateTime(2025, 6, 25), ChildDateOfBirth: new DateTime(2022, 1, 1)),
            (EligibilityCode: "40000000034", ValidityEndDate: new DateTime(2025, 8, 1), ChildDateOfBirth: new DateTime(2022, 1, 1)),
            (EligibilityCode: "40000000044", ValidityEndDate: new DateTime(2025, 6, 1), ChildDateOfBirth: new DateTime(2022, 1, 1)),
            (EligibilityCode: "40000000054", ValidityEndDate: new DateTime(2025, 7, 20), ChildDateOfBirth: new DateTime(2022, 1, 1)),
            (EligibilityCode: "40000000064", ValidityEndDate: new DateTime(2025, 6, 20), ChildDateOfBirth: new DateTime(2022, 1, 1))
        };

        foreach (var (eligibilityCode, validityEndDate, childDateOfBirth) in searchRecords)
        {
            var summaryId = Guid.NewGuid().ToString();
            var fosterCarerId = Guid.NewGuid();

            await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
            {
                FosterCarerId = fosterCarerId,
                FirstName = "Carer",
                LastName = eligibilityCode,
                NationalInsuranceNumber = $"AA{eligibilityCode[^6..]}A",
                LocalAuthorityID = 0
            });

            await _fakeInMemoryDb.WorkingFamiliesEventSummaries.AddAsync(new WorkingFamiliesEventSummary
            {
                WorkingFamiliesEventSummaryID = summaryId,
                EligibilityCode = eligibilityCode,
                ChildFirstName = "Child",
                ChildFirstNameTruncated = "Child",
                ChildPostCode = "NNU 1AE",
                ParentNationalInsuranceNumber = "AA123456A",
                ValidityStartDate = validityEndDate.AddMonths(-3),
                ValidityEndDate = validityEndDate,
                GracePeriodEndDate = validityEndDate.AddDays(14)
            });

            await _fakeInMemoryDb.FosterChildren.AddAsync(new FosterChild
            {
                FosterChildId = Guid.NewGuid(),
                FirstName = "Child",
                LastName = eligibilityCode,
                DateOfBirth = childDateOfBirth,
                PostCode = "NNU 1AE",
                EligibilityCode = eligibilityCode,
                FosterCarerId = fosterCarerId,
                WorkingFamiliesEventSummaryID = summaryId
            });
        }

        await _fakeInMemoryDb.SaveChangesAsync();
        var deterministicGateway = new FixedDateFosterFamiliesGateway(
            _fakeInMemoryDb,
            _mockWFEventGateway.Object,
            _mockLogger.Object,
            checkDate);

        // Act
        var firstPage = await deterministicGateway.SearchFosterFamilies(
            0,
            new FosterFamiliesSearchRequest { PageNumber = 1, PageSize = 3 });
        var secondPage = await deterministicGateway.SearchFosterFamilies(
            0,
            new FosterFamiliesSearchRequest { PageNumber = 2, PageSize = 3 });
        var thirdPage = await deterministicGateway.SearchFosterFamilies(
            0,
            new FosterFamiliesSearchRequest { PageNumber = 3, PageSize = 3 });
        var results = firstPage.Data.Concat(secondPage.Data).Concat(thirdPage.Data).ToList();

        // Assert
        new[] { firstPage.Data.Count(), secondPage.Data.Count(), thirdPage.Data.Count() }
            .Should().Equal(3, 3, 1);
        results.Select(item => item.ReconfirmationProperties.Status).Should().Equal(
            ReconfirmationStatus.Overdue,
            ReconfirmationStatus.Overdue,
            ReconfirmationStatus.Due,
            ReconfirmationStatus.Due,
            ReconfirmationStatus.NotDueYet,
            ReconfirmationStatus.NotDueYet,
            ReconfirmationStatus.ChildTooOld);
        results.Select(item => item.EligibilityCode).Should().Equal(
            "40000000014",
            "40000000044",
            "40000000024",
            "40000000064",
            "40000000034",
            "40000000054",
            "40000000004");
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Filter_By_Carer_Or_Partner_Nino()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);
        await _sut.CreateFosterFamily(request);

        var otherRequest = BuildValidRequest(DateTime.UtcNow);
        otherRequest.FosterCarer.CarerNationalInsuranceNumber = "QQ123456Q";
        await _sut.CreateFosterFamily(otherRequest);

        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 1,
                PageSize = 10,
                NINOFilter = request.FosterCarer.CarerNationalInsuranceNumber
            });

        // Assert
        result.TotalNumberOfRecords.Should().Be(1);
        result.Data.Should().HaveCount(1);
        result.Data.Single().CarerName.Should().Be("John Smith");
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Total_Record_Count()
    {
        // Arrange
        await _sut.CreateFosterFamily(BuildValidRequest(DateTime.UtcNow));
        await _sut.CreateFosterFamily(BuildValidRequest(DateTime.UtcNow));

        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

        // Assert
        result.TotalNumberOfRecords.Should().Be(2);
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Empty_Data_When_No_Records_Exist()
    {
        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

        // Assert
        result.TotalNumberOfRecords.Should().Be(0);
        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Grace_Period_End_Date()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

        // Assert
        var item = result.Data.Single();

        item.GracePeriodEndDate.Should().NotBe(default);
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Correct_Page()
    {
        // Arrange
        for (var i = 0; i < 15; i++)
        {
            await _sut.CreateFosterFamily(BuildValidRequest(DateTime.UtcNow));
        }

        // Act
        var result = await _sut.SearchFosterFamilies(
            0, new FosterFamiliesSearchRequest
            {
                PageNumber = 2,
                PageSize = 10
            });

        // Assert
        result.PageNumber.Should().Be(2);
        result.Data.Should().HaveCount(5);
    }

    #endregion

    #region Get Foster Child 

    [Test]
    public async Task GetFosterChild_Should_Use_CheckDate_For_TermValidity()
    {
        // Arrange
        // Create backdated foster appliaction for August 2026
        var fosterCarer = new FosterCarer
        {
            FosterCarerId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Smith",
            LocalAuthorityID = 0,
            NationalInsuranceNumber = "AA123456A",
            DateOfBirth = new DateTime(1980, 1, 1)
        };

        var fosterChild = new FosterChild
        {
            FosterChildId = Guid.NewGuid(),
            FosterCarerId = fosterCarer.FosterCarerId,
            FirstName = "Tom",
            LastName = "Smith",
            DateOfBirth = new DateTime(2025, 12, 1),
            PostCode = "NNU 1AE",
            EligibilityCode = "40000000001",
            ValidityStartDate = new DateTime(2026, 8, 20),
            ValidityEndDate = new DateTime(2026, 11, 20),
            SubmissionDate = new DateTime(2026, 8, 20),
            Status = "Active"
        };

        var workingEvent = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = Guid.NewGuid().ToString(),
            EligibilityCode = fosterChild.EligibilityCode,
            SubmissionDate = new DateTime(2026, 8, 20),
            ValidityStartDate = new DateTime(2026, 8, 20),
            ValidityEndDate = new DateTime(2026, 11, 20),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 31),
            GracePeriodEndDate = new DateTime(2027, 3, 31),
            ParentNationalInsuranceNumber = "AA123456A",
            ParentFirstName = "John",
            ParentLastName = "Smith",
            ParentDateOfBirth = new DateTime(1980, 1, 1),
            ChildFirstName = "Tom",
            ChildLastName = "Smith",
            ChildDateOfBirth = fosterChild.DateOfBirth,
            ChildPostCode = "NNU 1AE",
            CreatedDateTime = new DateTime(2026, 8, 20)
        };
        var workingEventSummary = new WorkingFamiliesEventSummary
        {
            WorkingFamiliesEventSummaryID = Guid.NewGuid().ToString(),
            EligibilityCode = fosterChild.EligibilityCode,
            LatestSubmissionDate = new DateTime(2026, 8, 20),
            ValidityStartDate = new DateTime(2026, 8, 20),
            ValidityEndDate = new DateTime(2026, 11, 20),
            DiscretionaryValidityStartDate = new DateTime(2026, 8, 31),
            GracePeriodEndDate = new DateTime(2027, 3, 31),
            GracePeriodEndDateApplied = true,
            ParentNationalInsuranceNumber = "AA123456A",
            ChildFirstName = "Tom-Boy",
            ChildFirstNameTruncated = "Tom",
            ChildDateOfBirth = fosterChild.DateOfBirth,
            ChildPostCode = "NNU 1AE"
        };
        fosterChild.WorkingFamiliesEventSummaryID = workingEventSummary.WorkingFamiliesEventSummaryID;

        await _fakeInMemoryDb.FosterCarers.AddAsync(fosterCarer);
        await _fakeInMemoryDb.FosterChildren.AddAsync(fosterChild);
        await _fakeInMemoryDb.WorkingFamiliesEvents.AddAsync(workingEvent);
        await _fakeInMemoryDb.WorkingFamiliesEventSummaries.AddAsync(workingEventSummary);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Simulate check date as September 2026
        var checkDate = new DateTime(2026, 9, 10);
        var deterministicGateway = new FixedDateFosterFamiliesGateway(_fakeInMemoryDb, _mockWFEventGateway.Object, _mockLogger.Object, checkDate);

        // Act
        var result = await deterministicGateway.GetFosterChild(fosterChild.FosterChildId, 0, false);

        // Assert
        result.TermValidity.Current.Should().NotBeNull();
        result.TermValidity.Current!.Name.Should().Be(TermName.Autumn);
        result.TermValidity.Next!.Name.Should().NotBe(TermName.Autumn);
        result.ChildTooYoung.Should().BeFalse();
    }

    [Test]
    public async Task GetFosterChild_Should_Return_FosterCarer_Details_When_Requested()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(
            fosterChildId,
            0,
            includeFosterCarer: true);

        // Assert
        result.CarerName.Should().Be("John Smith");
        result.PartnerName.Should().Be("Jane Smith");
    }

    [Test]
    public async Task GetFosterChild_Should_Return_FosterChild_Response()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, false);

        // Assert
        result.Should().NotBeNull();
        result.FosterChildId.Should().Be(fosterChildId);
    }

    [Test]
    public async Task GetFosterChild_Should_Return_Eligibility_Details()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
        result.ValidityStartDate.Should().NotBe(default);
    }

    [Test]
    public async Task GetFosterChild_Should_Return_Child_Details()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.ChildFirstName.Should().Be("Tom");
        result.ChildLastName.Should().Be("Smith");
        result.ChildDateOfBirth.Should().Be(new DateTime(2022, 1, 1));
        result.ChildPostCode.Should().Be("NNU 1AE");
    }

    [Test]
    public async Task GetFosterChild_Should_Return_Grace_Period_End_Date()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.GracePeriodEndDate.Should().NotBe(default);
    }

    [Test]
    public async Task GetFosterChild_Should_Throw_NotFoundException_When_Child_Does_Not_Exist()
    {
        // Act
        Func<Task> act = () =>
            _sut.GetFosterChild(Guid.NewGuid(), 0, false);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Create Foster Child

    [TestCase("ab 12 34 56 c", "AB123456C")]
    [TestCase("ab-12.34/56c", "AB-12.34/56C")]
    public async Task CreateFosterChild_Should_Preserve_Existing_Event_Nino_Formatting(
    string storedNino,
    string expectedEventNino)
    {
        var fosterCarerId = Guid.NewGuid();

        await _fakeInMemoryDb.FosterCarers.AddAsync(new FosterCarer
        {
            FosterCarerId = fosterCarerId,
            LocalAuthorityID = 0,
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateTime(1980, 1, 1),
            NationalInsuranceNumber = storedNino
        });
        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        var response = await _sut.CreateFosterChild(
            request, 0, fosterCarerId, DateTime.UtcNow);

        var workingEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .AsNoTracking()
            .SingleAsync(x => x.EligibilityCode == response.EligibilityCode);

        workingEvent.ParentNationalInsuranceNumber
            .Should().Be(expectedEventNino);

        var storedCarer = await _fakeInMemoryDb.FosterCarers
            .AsNoTracking()
            .SingleAsync(x => x.FosterCarerId == fosterCarerId);

        storedCarer.NationalInsuranceNumber.Should().Be(storedNino);
    }

    [Test]
    public async Task CreateFosterChild_Should_Create_FosterChild()
    {
        // Arrange
        var familyRequest = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(familyRequest);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        // Act
        await _sut.CreateFosterChild(
            request,
            0,
            fosterCarerId,
            DateTime.UtcNow);

        // Assert
        _fakeInMemoryDb.FosterChildren.Should().HaveCount(2);
    }

    [Test]
    public async Task CreateFosterChild_Should_Link_Child_To_FosterCarer()
    {
        // Arrange
        var familyRequest = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(familyRequest);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        // Act
        await _sut.CreateFosterChild(
            request,
            0,
            fosterCarerId,
            DateTime.UtcNow);

        // Assert
        var child = await _fakeInMemoryDb.FosterChildren
            .OrderByDescending(x => x.FosterChildId)
            .FirstAsync();

        child.FosterCarerId.Should().Be(fosterCarerId);
    }

    [Test]
    public async Task CreateFosterChild_Should_Create_WorkingFamilies_Event()
    {
        // Arrange
        var familyRequest = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(familyRequest);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        var existingEvents = await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync();

        // Act
        await _sut.CreateFosterChild(
            request,
            0,
            fosterCarerId,
            DateTime.UtcNow);

        // Assert
        (await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync())
            .Should()
            .Be(existingEvents + 1);
    }

    [Test]
    public async Task CreateFosterChild_Should_Return_Created_Response()
    {
        // Arrange
        var familyRequest = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(familyRequest);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        // Act
        var result = await _sut.CreateFosterChild(
            request,
            0,
            fosterCarerId,
            DateTime.UtcNow);

        // Assert
        result.ChildFirstName.Should().Be("Sam");
        result.ChildLastName.Should().Be("Jones");
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task CreateFosterChild_Should_Throw_NotFoundException_When_FosterCarer_Does_Not_Exist()
    {
        // Arrange
        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };
        Guid wrongGuid = Guid.NewGuid();

        // Act
        Func<Task> act = () =>
            _sut.CreateFosterChild(
                request,
                0,
                wrongGuid,
                DateTime.UtcNow);

        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster carer {wrongGuid} not found");
    }

    [Test]
    public async Task CreateFosterChild_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var familyRequest = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(familyRequest);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        var request = new FosterChildRequest
        {
            ChildFirstName = "Sam",
            ChildLastName = "Jones",
            ChildDateOfBirth = new DateTime(2023, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        // Act
        Func<Task> act = () => _sut.CreateFosterChild(
            request,
            123, // wrong LA
            fosterCarerId,
            DateTime.UtcNow);

        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster carer {fosterCarerId} not found");
    }

    #endregion

    #region Update Foster Child

    [Test]
    public async Task UpdateFosterChild_Should_Update_Child_Details()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest()
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        await _sut.UpdateFosterChild(
            fosterChildId,
            0,
            updateRequest);

        // Assert
        var child = await _fakeInMemoryDb.FosterChildren
            .SingleAsync(x => x.FosterChildId == fosterChildId);

        child.FirstName.Should().Be("Sam");
        child.LastName.Should().Be("Jones");
        child.DateOfBirth.Should().Be(new DateTime(2023, 1, 1));
        child.PostCode.Should().Be("AB1 2CD");
    }

    [Test]
    public async Task UpdateFosterChild_Should_Update_Updated_Date()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var child = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        var originalUpdated = child.Updated;

        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest()
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        await _sut.UpdateFosterChild(
            child.FosterChildId,
            0,
            updateRequest);

        // Assert
        var updated = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        updated.Updated.Should().BeAfter(originalUpdated);
    }

    [Test]
    public async Task UpdateFosterChild_Should_Return_Updated_Response()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest()
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        var result = await _sut.UpdateFosterChild(
            fosterChildId,
            0,
            updateRequest);

        // Assert
        result.ChildFirstName.Should().Be("Sam");
        result.ChildLastName.Should().Be("Jones");
    }

    [Test]
    public async Task UpdateFosterChild_Should_Update_Working_Families_Summary_And_Latest_Event()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChild = await _fakeInMemoryDb.FosterChildren
            .SingleAsync();

        var summary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        var latestEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        _mockWFEventGateway
            .Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(summary);

        _mockWFEventGateway
            .Setup(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode))
            .ReturnsAsync(latestEvent);

        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        var result = await _sut.UpdateFosterChild(
            fosterChild.FosterChildId,
            0,
            updateRequest);

        // Assert
        var updatedSummary = await _fakeInMemoryDb.WorkingFamiliesEventSummaries
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedSummary.ChildFirstName.Should().Be("Sam");
        updatedSummary.ChildFirstNameTruncated.Should().Be("sam");
        updatedSummary.ChildDateOfBirth.Should().Be(new DateTime(2023, 1, 1));
        updatedSummary.ChildPostCode.Should().Be("AB1 2CD");
        updatedSummary.LastUpdatedDate.Should().NotBe(default);

        var updatedEvent = await _fakeInMemoryDb.WorkingFamiliesEvents
            .SingleAsync(x => x.EligibilityCode == fosterChild.EligibilityCode);

        updatedEvent.ChildFirstName.Should().Be("Sam");
        updatedEvent.ChildLastName.Should().Be("Jones");
        updatedEvent.ChildDateOfBirth.Should().Be(new DateTime(2023, 1, 1));
        updatedEvent.ChildPostCode.Should().Be("AB1 2CD");

        result.ChildFirstName.Should().Be("Sam");
        result.ChildLastName.Should().Be("Jones");

        _mockWFEventGateway.Verify(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
        _mockWFEventGateway.Verify(g => g.GetLatestWorkingFamiliesEventByEligibilityCode(fosterChild.EligibilityCode), Times.Once);
    }

    [Test]
    public async Task UpdateFosterChild_Should_Throw_NotFoundException_When_Child_Does_Not_Exist()
    {
        // Arrange
        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest()
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };
        Guid wrongGuid = Guid.NewGuid();

        // Act
        Func<Task> act = () =>
            _sut.UpdateFosterChild(wrongGuid, 0, updateRequest);

        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster child {wrongGuid} not found");
    }

    [Test]
    public async Task UpdateFosterChild_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        var updateRequest = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest
            {
                ChildFirstName = "Sam",
                ChildLastName = "Jones",
                ChildDateOfBirth = new DateTime(2023, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        Func<Task> act = () => _sut.UpdateFosterChild(
            fosterChildId,
            123, // wrong LA
            updateRequest);

        // Assert
        await act.Should()
            .ThrowAsync<NotFoundException>()
            .WithMessage($"Foster child {fosterChildId} not found");
    }

    #endregion

    #region Reconfirm Foster Child

    [Test]
    public async Task ReconfirmFosterChild_Should_Create_Event_Update_Validity_Dates_And_Return_Response()
    {
        // Arrange
        var originalSubmissionDate = new DateTime(2025, 8, 26);
        var reconfirmationDate = new DateTime(2025, 11, 24);
        
        var expectedSummaryValidityStartDate = originalSubmissionDate;
        var expectedSummaryValidityEndDate = expectedSummaryValidityStartDate.AddMonths(6).AddDays(1);
        var expectedGPED = WorkingFamiliesEventHelper.GetGracePeriodEndDate(expectedSummaryValidityEndDate);

        await _sut.CreateFosterFamily(BuildValidRequest(originalSubmissionDate));
        var child = await _fakeInMemoryDb.FosterChildren.Include(x => x.WorkingFamiliesEventSummary).SingleAsync();

        var existingEventCount = await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync(x => x.EligibilityCode == child.EligibilityCode);
        _mockWFEventGateway.Setup(g => g.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(child.EligibilityCode)).ReturnsAsync(
            await _fakeInMemoryDb.WorkingFamiliesEventSummaries.FirstOrDefaultAsync(x => x.EligibilityCode == child.EligibilityCode)
        );
        _mockWFEventGateway.Setup(g => g.GetWorkingFamiliesEventsCount(child.EligibilityCode)).ReturnsAsync(
            await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync(x => x.EligibilityCode == child.EligibilityCode)
        );


        // Act
        var reconfirmedResponse = await _sut.ReconfirmFosterChild(child.FosterChildId, 0, reconfirmationDate);

        // Assert
        var updatedChild = await _fakeInMemoryDb.FosterChildren.SingleAsync(x => x.FosterChildId == child.FosterChildId);
        var reconfirmationEvent = await _fakeInMemoryDb.WorkingFamiliesEvents.OrderByDescending(x => x.CreatedDateTime).FirstOrDefaultAsync(x => x.SubmissionDate == reconfirmationDate);

        (await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync()).Should().Be(existingEventCount + 1);

        updatedChild.ValidityStartDate.Should().Be(expectedSummaryValidityStartDate);
        updatedChild.ValidityEndDate.Should().Be(expectedSummaryValidityEndDate);

        reconfirmationEvent.EligibilityCode.Should().Be(child.EligibilityCode);
        reconfirmationEvent.ParentFirstName.Should().Be("John");
        reconfirmationEvent.ParentLastName.Should().Be("Smith");
        reconfirmationEvent.ParentNationalInsuranceNumber.Should().Be(child.FosterCarer.NationalInsuranceNumber);
        reconfirmationEvent.ChildFirstName.Should().Be(child.FirstName);
        reconfirmationEvent.ChildLastName.Should().Be(child.LastName);
        reconfirmationEvent.ChildDateOfBirth.Should().Be(child.DateOfBirth);
        reconfirmationEvent.ChildPostCode.Should().Be(child.PostCode);
        reconfirmationEvent.CreatedDateTime.Should().NotBeNull();

        reconfirmedResponse.FosterChildId.Should().Be(child.FosterChildId);
        reconfirmedResponse.EligibilityCode.Should().Be(child.EligibilityCode);
        reconfirmedResponse.ValidityStartDate.Should().Be(expectedSummaryValidityStartDate);
        reconfirmedResponse.ValidityEndDate.Should().Be(expectedSummaryValidityEndDate);
        reconfirmedResponse.GracePeriodEndDate.Should().Be(expectedGPED);
        reconfirmedResponse.GracePeriodEndDateApplied.Should().BeTrue();
    }

    [Test]
    public async Task ReconfirmFosterChild_Should_Throw_NotFoundException_When_Child_Does_Not_Exist()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();

        // Act
        Func<Task> act = () => _sut.ReconfirmFosterChild(fosterChildId, 0, DateTime.UtcNow);

        // Assert
        await act.Should()
            .ThrowAsync<NotFoundException>()
            .WithMessage($"Foster child {fosterChildId} not found");
        (await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task ReconfirmFosterChild_Should_Throw_NotFoundException_When_Local_Authority_Does_Not_Match()
    {
        // Arrange
        await _sut.CreateFosterFamily(BuildValidRequest(DateTime.UtcNow));

        var child = await _fakeInMemoryDb.FosterChildren.SingleAsync();
        var existingEventCount = await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync();

        // Act
        Func<Task> act = () => _sut.ReconfirmFosterChild(child.FosterChildId, 123, DateTime.UtcNow);

        // Assert
        await act.Should()
            .ThrowAsync<NotFoundException>()
            .WithMessage("Foster child is not associated with selected local authority");
        (await _fakeInMemoryDb.WorkingFamiliesEvents.CountAsync()).Should().Be(existingEventCount);
    }

    #endregion

    #region Delete Foster Child

    [Test]
    public async Task DeleteFosterChild_Should_Delete_FosterChild()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        await _sut.DeleteFosterChild(fosterChildId, 0);

        // Assert
        _fakeInMemoryDb.FosterChildren.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteFosterChild_Should_Not_Delete_FosterCarer()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        await _sut.DeleteFosterChild(fosterChildId, 0);

        // Assert
        _fakeInMemoryDb.FosterCarers.Should().HaveCount(1);
    }

    [Test]
    public async Task DeleteFosterChild_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var request = BuildValidRequest(DateTime.UtcNow);

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        Func<Task> act = () => _sut.DeleteFosterChild(fosterChildId, 123); // wrong LA

        // Assert
        await act.Should()
             .ThrowAsync<NotFoundException>()
             .WithMessage($"Foster child {fosterChildId} not found");
    }

    [Test]
    public async Task DeleteFosterChild_Should_Throw_NotFoundException_When_Child_Does_Not_Exist()
    {
        // Act
        Func<Task> act = () =>
            _sut.DeleteFosterChild(Guid.NewGuid(), 0);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region EligibilityCode 

    [Test]
    public async Task GetEligibilityCodeForFosterChild_ReturnsNextAvailableCode()
    {
        // Act
        var result = await _sut.GetEligibilityCodeForFosterChild();

        // Assert
        result.Should().Be("40000000001");

        var range = await _fakeInMemoryDb.EligibilityCodeRanges.SingleAsync();
        range.NextAvailableCode.Should().Be(40000000002);
    }

    #endregion

    #region helpers

    private sealed class FailWorkingFamiliesEventSaveInterceptor
    : SaveChangesInterceptor
    {
        private readonly Exception _exception;

        public FailWorkingFamiliesEventSaveInterceptor(Exception exception)
        {
            _exception = exception;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var savingNewEvent = eventData.Context?.ChangeTracker
                .Entries<CheckYourEligibility.API.Domain.WorkingFamiliesEvent>()
                .Any(entry => entry.State == EntityState.Added) == true;

            if (savingNewEvent)
            {
                throw _exception;
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private static FosterFamilyRequest BuildValidRequest(DateTime submissionDate)
    {
        return new FosterFamilyRequest
        {
            HasPartner = true,
            SubmissionDate = submissionDate,

            FosterCarer = new FosterCarerRequest
            {
                CarerFirstName = "John",
                CarerLastName = "Smith",
                CarerDateOfBirth = new DateTime(1980, 1, 1),
                CarerNationalInsuranceNumber = GenerateValidNi(),
                LocalAuthorityID = 0
            },

            Partner = new FosterPartnerRequest
            {
                PartnerFirstName = "Jane",
                PartnerLastName = "Smith",
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                PartnerNationalInsuranceNumber = GenerateValidNi()
            },

            FosterChild = new FosterChildRequest
            {
                ChildFirstName = "Tom",
                ChildLastName = "Smith",
                ChildDateOfBirth = new DateTime(2022, 1, 1),
                ChildPostCode = "NNU 1AE"
            }
        };

    }

    private static string GenerateValidNi()
    {
        return $"AA{Random.Shared.Next(1_000_000):D6}A";
    }

    private sealed class FixedDateFosterFamiliesGateway(IEligibilityCheckContext db, IWorkingFamiliesEvent workingFamiliesEventGateway, ILogger<FosterFamiliesGateway> logger, DateTime checkDate)
        : FosterFamiliesGateway(db, workingFamiliesEventGateway, logger)
    {
        private readonly DateTime _checkDate = checkDate;

        protected override DateTime GetCheckDate() => _checkDate;
    }

    #endregion
}