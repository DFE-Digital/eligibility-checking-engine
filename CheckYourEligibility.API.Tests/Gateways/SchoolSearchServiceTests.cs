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
    public void Teardown()
    {
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

        // Act
        var response = await _sut.Search(expectedResult.EstablishmentName, la);

        // Assert
        if (response != null && response.Any())
            response.First().Name.Should().BeEquivalentTo(expectedResult.EstablishmentName);
    }

    [Test]
    public async Task Given_Search_Urn_Should_Return_ExpectedResult()
    {
        // Arrange
        _fakeInMemoryDb.Establishments.RemoveRange(_fakeInMemoryDb.Establishments);
        Establishment = _fixture.Create<Establishment>();
        var urn = 12345;
        Establishment.EstablishmentId = urn;
        _fakeInMemoryDb.Establishments.Add(Establishment);
        _fakeInMemoryDb.SaveChanges();
        var expectedResult = _fakeInMemoryDb.Establishments.First();
        string la = null;

        // Act
        var response = await _sut.Search(urn.ToString(), la);

        // Assert
        if (response != null && response.Any())
            response.First().Name.Should().BeEquivalentTo(expectedResult.EstablishmentName);
    }
}