using AutoFixture;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework.Internal;

namespace CheckYourEligibility.Core.Tests.Gateways;

public class EstablishmentManagementGatewayTests : TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private EstablishmentManagementGateway _sut;

    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(EstablishmentManagementGatewayTests),
                InMemoryDatabaseRoot)
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _fakeInMemoryDb.Database.EnsureDeleted();
        _fakeInMemoryDb.Database.EnsureCreated();

        _sut = new EstablishmentManagementGateway(_fakeInMemoryDb);
    }

    [TearDown]
    public async Task Teardown()
    {
        _fakeInMemoryDb.Establishments.RemoveRange(
            _fakeInMemoryDb.Establishments);

        _fakeInMemoryDb.LocalAuthorities.RemoveRange(
            _fakeInMemoryDb.LocalAuthorities);

        _fakeInMemoryDb.MultiAcademyTrusts.RemoveRange(
            _fakeInMemoryDb.MultiAcademyTrusts);

        _fakeInMemoryDb.MultiAcademyTrustEstablishments.RemoveRange(
            _fakeInMemoryDb.MultiAcademyTrustEstablishments);

        await _fakeInMemoryDb.SaveChangesAsync();
    }

    [Test]
    public async Task Given_Search_With_Urn_Should_Return_Expected_Result()
    {
        // Arrange
        var establishment = _fixture.Create<Domain.Establishment>();

        establishment.EstablishmentID = 12345;

        _fakeInMemoryDb.Establishments.Add(establishment);
        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new SupportEstablishmentSearchRequest
        {
            Urn = 12345,
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.Search(request);

        // Assert
        result.Data.Should().ContainSingle();
        result.Data.First().Id.Should().Be(12345);
    }

    [Test]
    public async Task Given_Search_With_Name_Should_Return_Expected_Result()
    {
        // Arrange
        var establishment = _fixture.Create<Domain.Establishment>();

        establishment.EstablishmentName = "Test School";

        _fakeInMemoryDb.Establishments.Add(establishment);

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new SupportEstablishmentSearchRequest
        {
            Name = "Test",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.Search(request);

        // Assert
        result.Data.Should().ContainSingle();
        result.Data.First().Name.Should().Be("Test School");
    }

    [Test]
    public async Task Given_Search_With_Type_Should_Return_Expected_Result()
    {
        // Arrange
        var establishment = _fixture.Create<Domain.Establishment>();

        establishment.Type = "Academy";

        _fakeInMemoryDb.Establishments.Add(establishment);

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new SupportEstablishmentSearchRequest
        {
            Type = "Academy",
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.Search(request);

        // Assert
        result.Data.Should().ContainSingle();
        result.Data.First().Type.Should().Be("Academy");
    }

    [Test]
    public async Task Given_Search_Should_Return_Paging_Information()
    {
        // Arrange
        var establishments = _fixture.CreateMany<Domain.Establishment>(15);

        _fakeInMemoryDb.Establishments.AddRange(establishments);

        await _fakeInMemoryDb.SaveChangesAsync();

        var request = new SupportEstablishmentSearchRequest
        {
            PageNumber = 2,
            PageSize = 10
        };

        // Act
        var result = await _sut.Search(request);

        // Assert
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalNumberOfRecords.Should().Be(15);
    }

    [Test]
    public async Task Given_Get_When_Not_Found_Should_Return_Null()
    {
        // Act
        var result = await _sut.Get(99999);

        // Assert
        result.Should().BeNull();
    }
    [Test]
    public async Task Given_Get_Should_Return_MultiAcademyTrust()
    {
        // Arrange
        var authority = _fixture.Create<LocalAuthority>();

        _fakeInMemoryDb.LocalAuthorities.Add(authority);

        var establishment = _fixture.Create<Domain.Establishment>();

        establishment.LocalAuthority = authority;

        _fakeInMemoryDb.Establishments.Add(establishment);

        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = 1,
            Name = "Test MAT"
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);

        _fakeInMemoryDb.MultiAcademyTrustEstablishments.Add(
            new MultiAcademyTrustEstablishment
            {
                EstablishmentID = establishment.EstablishmentID,
                MultiAcademyTrustID = 1
            });

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.Get(establishment.EstablishmentID);

        // Assert
        result!.MultiAcademyTrust.Should().NotBeNull();
        result.MultiAcademyTrust.Name.Should().Be("Test MAT");
    }
}