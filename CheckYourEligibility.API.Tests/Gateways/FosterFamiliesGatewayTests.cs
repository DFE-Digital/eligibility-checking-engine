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
            FirstName = "John",
            LastName = "Smith",
            NationalInsuranceNumber = "NN123456C"
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
        var result = await _sut.GetFosterFamily(fosterCarerId, true);

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
            NationalInsuranceNumber = "NN123456C"
        };

        _fakeInMemoryDb.FosterCarers.Add(fosterCarer);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetFosterFamily(fosterCarerId, false);

        // Assert
        result.FosterChildren.Should().BeEmpty();
    }

    [Test]
    public async Task GetFosterFamily_Should_Return_Not_Found_Exception()
    {
        // Act
        Func<Task> act = async () => await _sut.GetFosterFamily(Guid.NewGuid());

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
        result.EligiblityCode.Should().NotBeNullOrWhiteSpace();
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

        fosterChild.EligibilityCode.Should().Be(response.EligiblityCode);
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
        await _sut.UpdateFosterCarer(fosterCarerId, request);

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
        await _sut.UpdateFosterCarer(fosterCarerId, request);

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
        await _sut.UpdateFosterCarer(fosterCarerId, request);

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
            _sut.UpdateFosterCarer(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
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
        await _sut.DeleteFosterCarer(fosterCarerId);

        // Assert
        _fakeInMemoryDb.FosterCarers.Should().BeEmpty();
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
        await _sut.DeleteFosterPartner(fosterCarerId);

        // Assert
        var fosterCarer = await _fakeInMemoryDb.FosterCarers.SingleAsync();

        fosterCarer.HasPartner.Should().BeFalse();
        fosterCarer.PartnerFirstName.Should().BeNull();
        fosterCarer.PartnerLastName.Should().BeNull();
        fosterCarer.PartnerDateOfBirth.Should().BeNull();
        fosterCarer.PartnerNationalInsuranceNumber.Should().BeNull();
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
            new FosterFamiliesSearchRequest
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
            new FosterFamiliesSearchRequest
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
            new FosterFamiliesSearchRequest
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
            new FosterFamiliesSearchRequest
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
            new FosterFamiliesSearchRequest
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
    public async Task GetFosterChild_Should_Return_FosterChild_Response()
    {
        // Arrange
        var request = BuildValidRequest();

        await _sut.CreateFosterFamily(request);

        var fosterChildId = await _fakeInMemoryDb.FosterChildren
            .Select(x => x.FosterChildId)
            .SingleAsync();

        // Act
        var result = await _sut.GetFosterChild(fosterChildId);

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
        var result = await _sut.GetFosterChild(fosterChildId);

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
        var result = await _sut.GetFosterChild(fosterChildId);

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
        var result = await _sut.GetFosterChild(fosterChildId);

        // Assert
        result.GracePeriodEnds.Should().NotBe(default);
    }

    [Test]
    public async Task GetFosterChild_Should_Throw_NotFoundException_When_Child_Does_Not_Exist()
    {
        // Act
        Func<Task> act = () =>
            _sut.GetFosterChild(Guid.NewGuid());

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
                CarerNationalInsuranceNumber = "NN123456C"
            },

            Partner = new FosterPartnerRequest
            {
                PartnerFirstName = "Jane",
                PartnerLastName = "Smith",
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                PartnerNationalInsuranceNumber = "AB123456C"
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

    #endregion
}