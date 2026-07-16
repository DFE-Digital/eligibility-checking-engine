using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework.Internal;

namespace CheckYourEligibility.API.Tests;

public class LocalAuthorityGatewayTests : TestBase.TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private IEligibilityCheckContext _fakeInMemoryDb;
    private LocalAuthorityGateway _sut;
    private Mock<ILogger<LocalAuthorityGateway>> _mockLogger = null!;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(LocalAuthorityGatewayTests),
                InMemoryDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId
                    .TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _mockLogger = new Mock<ILogger<LocalAuthorityGateway>>();

        _sut = new LocalAuthorityGateway(
            _fakeInMemoryDb,
            _mockLogger.Object);
    }

    [TearDown]
    public async Task Teardown()
    {
        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task GetEstablishmentsByLocalAuthorityId_WhenLaExists_ReturnsEstablishments()
    {
        // Arrange
        var la = new LocalAuthority
        {
            LaName = "testname",
            LocalAuthorityID = 100,
        };

        var establishment = new Establishment
        {
            EstablishmentID = 1,
            EstablishmentName = "Test School",
            LocalAuthorityID = 100,
            County = "Test County",
            Locality = "Test Locality",
            Postcode = "AB1 2CD",
            Street = "123 Test Street",
            Town = "Test Town",
            Type = "School"
        };

        _fakeInMemoryDb.LocalAuthorities.Add(la);
        _fakeInMemoryDb.Establishments.Add(establishment);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetEstablishmentsByLocalAuthorityId(100);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);

        result[0].URN.Should().Be(1);
        result[0].Name.Should().Be("Test School");
    }

    [Test]
    public async Task GetEstablishmentsByLocalAuthorityId_WhenLaExistsButNoEstablishments_ReturnsEmptyList()
    {
        // Arrange
        var la = new LocalAuthority
        {
            LaName = "testname",
            LocalAuthorityID = 100,
        };

        _fakeInMemoryDb.LocalAuthorities.Add(la);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetEstablishmentsByLocalAuthorityId(100);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public void GetEstablishmentsByLocalAuthorityId_WhenLaDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var invalidLaId = 999;

        // Act
        Func<Task> act = async () =>
            await _sut.GetEstablishmentsByLocalAuthorityId(invalidLaId);

        // Assert
        act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Unable to find establishments: - {invalidLaId}*");
    }
}