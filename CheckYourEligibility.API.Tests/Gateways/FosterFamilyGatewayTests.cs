using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Data.Mappings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework.Internal;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class FosterFamilyGatewayTests : TestBase.TestBase
{
    private new Fixture _fixture = null!;
    private Mock<ILogger<FosterFamilyGateway>> _mockLogger = null!;
    private IConfiguration _configuration = null!;
    private EligibilityCheckContext _dbContext = null!;

    private FosterFamilyGateway _sut = null!;
    private FosterFamilyRequest _testFosterFamily = null!;
    private FosterFamilyUpdateRequest _testFosterFamilyUpdateRequest = null!;
    private string myGuid = null!;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _mockLogger = new Mock<ILogger<FosterFamilyGateway>>();

        _testFosterFamily = new FosterFamilyRequest()
        {
            Data = new FosterFamilyRequestData
            {
                CarerFirstName = "John",
                CarerLastName = "Doe",
                CarerDateOfBirth = new DateTime(1980, 5, 15),
                CarerNationalInsuranceNumber = "AB123456C",
                HasPartner = false,
                PartnerFirstName = null,
                PartnerLastName = null,
                PartnerDateOfBirth = null,
                PartnerNationalInsuranceNumber = null,
                ChildFirstName = "Emily",
                ChildLastName = "Doe",
                ChildDateOfBirth = new DateTime(2015, 3, 10),
                ChildPostCode = "SW1A 1AA",
                SubmissionDate = DateTime.UtcNow
            }
        };

        myGuid = "ee0e0128-9887-4482-8df4-7922b8c8f53a";
        _testFosterFamilyUpdateRequest =
                new FosterFamilyUpdateRequest
                {
                    CarerFirstName = "Sarah",
                    CarerLastName = "Thompson",
                    CarerDateOfBirth = new DateTime(1985, 4, 12),
                    CarerNationalInsuranceNumber = "AB123456C",

                    HasPartner = true,

                    PartnerFirstName = "David",
                    PartnerLastName = "Thompson",
                    PartnerDateOfBirth = new DateTime(1983, 11, 2),
                    PartnerNationalInsuranceNumber = "ZX987654A",

                    ChildFirstName = "Emily",
                    ChildLastName = "Thompson",
                    ChildDateOfBirth = new DateTime(2015, 6, 21),
                    ChildPostCode = "LS12 4AB"
                };

        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var mapper = config.CreateMapper();

        _sut = new FosterFamilyGateway(
            mapper,
            _dbContext,
            _mockLogger.Object
        );

    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    #region PostFosterFamily

    [Test]
    public void PostFosterFamily_NullData_ThrowsArgumentNullException()
    {
        // Act + Assert
        var ex = Assert.ThrowsAsync<ArgumentNullException>(() => _sut.PostFosterFamily(null!));
        Assert.That(ex!.ParamName, Is.EqualTo("data"));
    }

    [Test]
    public async Task PostFosterFamily_MapperReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var nullReturningMapper = new Mock<IMapper>();
        nullReturningMapper
            .Setup(m => m.Map<FosterCarer>(It.IsAny<FosterFamilyRequestData>()))
            .Returns((FosterCarer)null!);

        var localSut = new FosterFamilyGateway(
            nullReturningMapper.Object,
            _dbContext,
            _mockLogger.Object
        );

        // Act + Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => localSut.PostFosterFamily(_testFosterFamily.Data));
        Assert.That(ex!.Message, Is.EqualTo("Mapping to FosterCarer returned null."));
    }

    #endregion

    #region Get Foster Family

    [Test]
    public async Task GetFosterFamily_ExistingId_ReturnsFosterFamilyResponse()
    {
        // Arrange
        var fosterCarer = new FosterCarer
        {
            FosterCarerId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(2015, 12, 25),
            NationalInsuranceNumber = "AB123456C",
            HasPartner = false,
            FosterChild = new FosterChild
            {
                FirstName = "Emily",
                LastName = "Doe",
                DateOfBirth = new DateTime(2015, 3, 10),
                PostCode = "SW1A 1AA",
                EligibilityCode = "123456789"
            },
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };

        _dbContext.FosterCarers.Add(fosterCarer);
        await _dbContext.SaveChangesAsync();

        // Act
        var actionResult = await _sut.GetFosterFamily(fosterCarer.FosterCarerId.ToString());

        // Assert
        Assert.That(actionResult, Is.TypeOf<FosterFamilyResponse>());

    }

    [Test]
    public async Task GetFosterFamily_NonExistingId_ReturnsNull()
    {
        // Act
        var actionResult = await _sut.GetFosterFamily(Guid.NewGuid().ToString());

        // Assert
        Assert.That(actionResult, Is.Null);
    }

    #endregion

}