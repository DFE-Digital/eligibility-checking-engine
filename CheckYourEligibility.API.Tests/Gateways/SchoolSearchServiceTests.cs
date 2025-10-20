// Ignore Spelling: Levenshtein

using AutoFixture;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests;

public class SchoolSearchServiceTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private EstablishmentSearchGateway _sut;
    private Establishment Establishment;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;
        _fakeInMemoryDb = new EligibilityCheckContext(options);
        _sut = new EstablishmentSearchGateway(new NullLoggerFactory(), _fakeInMemoryDb);
    }

    [TearDown]
    public async Task Teardown()
    {
        _fakeInMemoryDb.Establishments.RemoveRange(_fakeInMemoryDb.Establishments);
        _fakeInMemoryDb.LocalAuthorities.RemoveRange(_fakeInMemoryDb.LocalAuthorities);
        _fakeInMemoryDb.MultiAcademyTrusts.RemoveRange(_fakeInMemoryDb.MultiAcademyTrusts);
        _fakeInMemoryDb.MultiAcademyTrustSchools.RemoveRange(_fakeInMemoryDb.MultiAcademyTrustSchools);
        await _fakeInMemoryDb.SaveChangesAsync();
    }

    [Test]
    public async Task Given_Search_Should_Return_ExpectedResult()
    {
        // Arrange
        _fakeInMemoryDb.Establishments.RemoveRange(_fakeInMemoryDb.Establishments);
        Establishment = _fixture.Create<Establishment>();
        _fakeInMemoryDb.Establishments.Add(Establishment);
        _fakeInMemoryDb.SaveChanges();
        var expectedResult = _fakeInMemoryDb.Establishments.First();
        string la = null;
        string mat = null;

        // Act
        var response = await _sut.Search(expectedResult.EstablishmentName, la, mat);

        // Assert
        response.First().Name.Should().BeEquivalentTo(expectedResult.EstablishmentName);
    }

    [Test]
    public async Task Given_Search_Urn_Should_Return_ExpectedResult()
    {
        // Arrange
        Establishment = _fixture.Create<Establishment>();
        var urn = 12345;
        Establishment.EstablishmentId = urn;
        Establishment.StatusOpen = true;
        _fakeInMemoryDb.Establishments.Add(Establishment);
        _fakeInMemoryDb.SaveChanges();
        var expectedResult = _fakeInMemoryDb.Establishments.First();
        string la = null;
        string mat = null;

        // Act
        var response = await _sut.Search(urn.ToString(), la, mat);

        // Assert
        response.First().Name.Should().BeEquivalentTo(expectedResult.EstablishmentName);
    }

    [Test]
    public async Task Given_Search_Urn_OutsideLA_Should_Not_Return_Result()
    {
        // Arrange
        Establishment = _fixture.Create<Establishment>();
        var urn = 12345;
        Establishment.EstablishmentId = urn;
        Establishment.LocalAuthorityId = 1;
        _fakeInMemoryDb.Establishments.Add(Establishment);
        _fakeInMemoryDb.SaveChanges();
        string la = "2";
        string mat = null;

        // Act
        var response = await _sut.Search(urn.ToString(), la, mat);

        // Assert
        response.Count().Should().Be(0);
    }

    [Test]
    public async Task Given_Search_Urn_OutsideMAT_Should_Not_Return_Result()
    {
        // Arrange
        Establishment = _fixture.Create<Establishment>();
        var urn = 12345;
        Establishment.EstablishmentId = urn;
        _fakeInMemoryDb.Establishments.Add(Establishment);
        var multiAcademyTrustSchool = _fixture.Create<MultiAcademyTrustSchool>();
        multiAcademyTrustSchool.SchoolId = Establishment.EstablishmentId;
        multiAcademyTrustSchool.MultiAcademyTrust.UID = 1;
        multiAcademyTrustSchool.TrustId = 1;
        _fakeInMemoryDb.MultiAcademyTrustSchools.Add(multiAcademyTrustSchool);
        _fakeInMemoryDb.SaveChanges();
        string la = null;
        string mat = "2";

        // Act
        var response = await _sut.Search(urn.ToString(), la, mat);

        // Assert
        response.Count().Should().Be(0);
    }
        
    [Test]
    public async Task Given_Search_With_LA_Should_Return_ExpectedResult()
    {
        // Arrange
        var primarySchool = _fixture.Create<Establishment>();
        primarySchool.EstablishmentName = "primary school";
        primarySchool.LocalAuthority.LocalAuthorityId = 1;
        var secondarySchool = _fixture.Create<Establishment>();
        secondarySchool.EstablishmentName = "secondary school";
        secondarySchool.LocalAuthority.LocalAuthorityId = 2;
        secondarySchool.StatusOpen = true;

        _fakeInMemoryDb.Establishments.Add(primarySchool);
        _fakeInMemoryDb.Establishments.Add(secondarySchool);
        _fakeInMemoryDb.SaveChanges();
        string la = "2";
        string mat = null;

        // Act
        var response = await _sut.Search("school", la, mat);

        // Assert
        response.First().Name.Should().BeEquivalentTo(secondarySchool.EstablishmentName);
    }

    [Test]
    public async Task Given_Search_With_MAT_Should_Return_ExpectedResult()
    {
        // Arrange
        var primarySchool = _fixture.Create<Establishment>();
        primarySchool.EstablishmentName = "primary school";
        primarySchool.LocalAuthority.LocalAuthorityId = 1;
        var secondarySchool = _fixture.Create<Establishment>();
        secondarySchool.EstablishmentName = "secondary school";
        secondarySchool.LocalAuthority.LocalAuthorityId = 2;
        secondarySchool.StatusOpen = true;
        _fakeInMemoryDb.Establishments.Add(primarySchool);
        _fakeInMemoryDb.SaveChanges();
        _fakeInMemoryDb.Establishments.Add(secondarySchool);
        _fakeInMemoryDb.SaveChanges();

        var multiAcademyTrustSchool = _fixture.Create<MultiAcademyTrustSchool>();
        multiAcademyTrustSchool.SchoolId = secondarySchool.EstablishmentId;
        multiAcademyTrustSchool.MultiAcademyTrust.UID = 1;
        multiAcademyTrustSchool.TrustId = 1;
        _fakeInMemoryDb.MultiAcademyTrustSchools.Add(multiAcademyTrustSchool);

        _fakeInMemoryDb.SaveChanges();
        string la = null;
        string mat = "1";

        // Act
        var response = await _sut.Search("school", la, mat);

        // Assert
        response.First().Name.Should().BeEquivalentTo(secondarySchool.EstablishmentName);
    }
}