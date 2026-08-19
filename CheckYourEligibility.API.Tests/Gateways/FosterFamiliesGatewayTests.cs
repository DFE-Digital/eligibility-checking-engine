using CheckYourEligibility.API.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.Gateways;

public class FosterFamiliesGatewayTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private FosterFamiliesGateway _sut;
    private Mock<ILogger<FosterFamiliesGateway>> _mockLogger = null!;
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public async Task SetUpAsync()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(nameof(EligibilityCheckReportingGatewayTests), InMemoryDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _mockLogger = new Mock<ILogger<FosterFamiliesGateway>>();

        // Ensure database is created and clean
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _sut = new FosterFamiliesGateway(_fakeInMemoryDb, _mockLogger.Object);
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
    public async Task CreateFosterFamily_Should_Return_Created_Response()
    {
        // Arrange
        var request = BuildValidRequest();

        // Act
        var result = await _sut.CreateFosterFamily(request);

        // Assert
        result.Should().NotBeNull();
        result.ChildName.Should().Be("Tom Smith");
        result.Status.Should().Be("Active");
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task CreateFosterFamily_Should_Return_Created_Response_But_Different_LA_same_NINO()
    {
        // Arrange
        // LA is 0 
        var request1 = BuildValidRequest();
        string request1NINO = request1.FosterCarer.CarerNationalInsuranceNumber;
        await _sut.CreateFosterFamily(request1);

        // Act
        // LA is now 123 but NINO is same
        var request2 = BuildValidRequest();
        request2.FosterCarer.LocalAuthorityID = 123;
        request2.FosterCarer.CarerNationalInsuranceNumber = request1NINO;
        var result = await _sut.CreateFosterFamily(request2);

        // Assert
        result.Should().NotBeNull();
        result.ChildName.Should().Be("Tom Smith");
        result.Status.Should().Be("Active");
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task CreateFosterFamily_Should_Link_Child_To_FosterCarer()
    {
        // Arrange
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        _fakeInMemoryDb.WorkingFamiliesEvents.Should().HaveCount(1);
    }

    [Test]
    public async Task CreateFosterFamily_Should_Set_EligibilityCode_On_Child()
    {
        // Arrange
        var request = BuildValidRequest();

        // Act
        var response = await _sut.CreateFosterFamily(request);

        // Assert
        var fosterChild = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        fosterChild.EligibilityCode.Should().Be(response.EligibilityCode);
    }

    [Test]
    public async Task CreateFosterFamily_Should_Set_Validity_Dates()
    {
        // Arrange
        var request = BuildValidRequest();

        // Act
        await _sut.CreateFosterFamily(request);

        // Assert
        var fosterChild = await _fakeInMemoryDb.FosterChildren.SingleAsync();

        fosterChild.ValidityStartDate.Should().NotBe(default);
        fosterChild.ValidityEndDate.Should().NotBe(default);
        fosterChild.ValidityEndDate.Should().BeAfter(fosterChild.ValidityStartDate);
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
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        // Act
        Func<Task> act = () => _sut.CreateFosterFamily(request);

        // Assert
        await act.Should()
            .ThrowAsync<ValidationException>()
            .WithMessage($"*{request.FosterCarer.CarerNationalInsuranceNumber}*already exists*");
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
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
    public async Task DeleteFosterPartner_Should_Remove_Partner_Details()
    {
        // Arrange
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        var fosterCarerId = await _fakeInMemoryDb.FosterCarers
            .Select(x => x.FosterCarerId)
            .SingleAsync();

        // Act
        await _sut.DeleteFosterPartner(fosterCarerId, 0);

        // Assert
        var fosterCarer = await _fakeInMemoryDb.FosterCarers.SingleAsync();

        fosterCarer.HasPartner.Should().BeFalse();
        fosterCarer.PartnerFirstName.Should().BeNull();
        fosterCarer.PartnerLastName.Should().BeNull();
        fosterCarer.PartnerDateOfBirth.Should().BeNull();
        fosterCarer.PartnerNationalInsuranceNumber.Should().BeNull();
    }

    [Test]
    public async Task DeleteFosterPartner_Should_Throw_NotFound_When_LA_Does_Not_Match()
    {
        // Arrange
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
    public async Task SearchFosterFamilies_Should_Return_Total_Record_Count()
    {
        // Arrange
        await _sut.CreateFosterFamily(BuildValidRequest());
        await _sut.CreateFosterFamily(BuildValidRequest());

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
        var request = BuildValidRequest();

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

        item.GracePeriodEnds.Should().NotBe(default);
    }

    [Test]
    public async Task SearchFosterFamilies_Should_Return_Correct_Page()
    {
        // Arrange
        for (var i = 0; i < 15; i++)
        {
            await _sut.CreateFosterFamily(BuildValidRequest());
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
    public async Task GetFosterChild_Should_Return_FosterCarer_Details_When_Requested()
    {
        // Arrange
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
        result.EligibilityConfirmedOn.Should().NotBe(default);
    }

    [Test]
    public async Task GetFosterChild_Should_Return_Child_Details()
    {
        // Arrange
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.ChildFullName.Should().Be("Tom Smith");
        result.ChildDateOfBirth.Should().Be(new DateTime(2022, 1, 1));
        result.PostCode.Should().Be("NNU 1AE");
    }

    [Test]
    public async Task GetFosterChild_Should_Return_Grace_Period_End_Date()
    {
        // Arrange
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId, 0, true);

        // Assert
        result.GracePeriodEnds.Should().NotBe(default);
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

    [Test]
    public async Task CreateFosterChild_Should_Create_FosterChild()
    {
        // Arrange
        var familyRequest = BuildValidRequest();

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
        var familyRequest = BuildValidRequest();

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
        var familyRequest = BuildValidRequest();

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
        var familyRequest = BuildValidRequest();

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
        result.ChildName.Should().Be("Sam Jones");
        result.EligibilityCode.Should().NotBeNullOrWhiteSpace();
        result.Status.Should().Be("");
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
        var familyRequest = BuildValidRequest();

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
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
        result.ChildFullName.Should().Be("Sam Jones");
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
        var request = BuildValidRequest();

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

    #region Delete Foster Child

    [Test]
    public async Task DeleteFosterChild_Should_Delete_FosterChild()
    {
        // Arrange
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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
        var request = BuildValidRequest();

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

    #region helpers

    private static FosterFamilyRequest BuildValidRequest()
    {
        return new FosterFamilyRequest
        {
            HasPartner = true,
            SubmissionDate = DateTime.UtcNow,

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

    #endregion
}