// Ignore Spelling: Levenshtein

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.CsvImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests;

[ExcludeFromCodeCoverage]
public class AdministrationServiceTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IMapper _mapper;
    private AdministrationGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";


        _sut = new AdministrationGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public void Given_CleanUpEligibilityChecks_Should_Return_Pass()
    {
        // Arrange

        // Act
        _sut.CleanUpEligibilityChecks();

        // Assert
        Assert.Pass();
    }

    [Test]
    public void Given_ImportEstablishments_Should_Return_Pass()
    {
        var data = _fixture.CreateMany<EstablishmentRow>().ToList();
        //Make a duplicate la
        var existingData = data.First();
        var la = new LocalAuthority
        {
            LocalAuthorityId = existingData.LaCode,
            LaName = existingData.LaName
        };
        _fakeInMemoryDb.LocalAuthorities.Add(la);
        _fakeInMemoryDb.Establishments.Add(new Establishment
        {
            EstablishmentId = existingData.Urn,
            EstablishmentName = existingData.EstablishmentName,
            LocalAuthority = la,
            County = existingData.County,
            Postcode = existingData.Postcode,
            Locality = existingData.Locality,
            Street = existingData.Street,
            Town = existingData.Town,
            StatusOpen = true,
            Type = existingData.Type
        });

        _fakeInMemoryDb.SaveChanges();

        // Act
        _sut.ImportEstablishments(data);

        // Assert
        Assert.Pass();
    }

    /// <summary>
    ///     Calling multiple times will generate concurrency errors, which is a limitation of in memory db
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task Given_ImportEstablishments_DuplicatesShould_Return_Pass()
    {
        // Arrange
        var data = _fixture.CreateMany<EstablishmentRow>();

        // Act
        await _sut.ImportEstablishments(data);

        // Assert
        Assert.Pass();
    }

    [Test]
    public void Given_ImportHomeOfficeData_Should_Return_Pass()
    {
        // Arrange
        var data = _fixture.CreateMany<FreeSchoolMealsHO>();

        // Act
        _sut.ImportHomeOfficeData(data);

        // Assert
        Assert.Pass();
    }

    [Test]
    public void Given_ImportHMRCData_Should_Return_Pass()
    {
        // Arrange
        var data = _fixture.CreateMany<FreeSchoolMealsHMRC>();

        // Act
        _sut.ImportHMRCData(data);

        // Assert
        Assert.Pass();
    }
    
    [Test]
    public async Task Given_ImportWfHMRCData_Should_Return_Pass()
    {
        // Arrange
        var data = _fixture.CreateMany<WorkingFamiliesEvent>();

        // Act
        await _sut.ImportWfHMRCData(data);

        // Assert
        Assert.Pass();
    }
}