using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Data.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class CreateApplicationUseCaseTests
{
    private new Fixture _fixture = null!;
    private Mock<ILogger<FosterFamilyGateway>> _mockLogger = null!;
    private IConfiguration _configuration = null!;
    private EligibilityCheckContext _dbContext = null!;

    private FosterFamilyGateway _sut = null!;
    private FosterFamilyRequest _testFosterFamily = null!;

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
                CarerDateOfBirth = new DateOnly(1980, 5, 15),
                CarerNationalInsuranceNumber = "AB123456C",
                HasPartner = false,
                PartnerFirstName = null,
                PartnerLastName = null,
                PartnerDateOfBirth = null,
                PartnerNationalInsuranceNumber = null,
                ChildFirstName = "Emily",
                ChildLastName = "Doe",
                ChildDateOfBirth = new DateOnly(2015, 3, 10),
                ChildPostCode = "SW1A 1AA",
                SubmissionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            }
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
}