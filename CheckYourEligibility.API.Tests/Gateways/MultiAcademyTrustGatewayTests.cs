using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class MultiAcademyTrustGatewayTests : TestBase.TestBase
{
    private static readonly InMemoryDatabaseRoot InMemoryDatabaseRoot = new();

    private IEligibilityCheckContext _fakeInMemoryDb;
    private MultiAcademyTrustGateway _sut;
    private Mock<ILogger<MultiAcademyTrustGateway>> _mockLogger = null!;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(
                nameof(MultiAcademyTrustGatewayTests),
                InMemoryDatabaseRoot)
            .ConfigureWarnings(x => x.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId
                    .TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _mockLogger = new Mock<ILogger<MultiAcademyTrustGateway>>();

        _sut = new MultiAcademyTrustGateway(
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
    public async Task GetMultiAcademyTrustById_WhenMatExists_ReturnsMat()
    {
        // Arrange
        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = 123,
            Name = "Test MAT",
            AcademyCanReviewEvidence = true
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetMultiAcademyTrustById(123);

        // Assert
        result.Should().NotBeNull();
        result!.MultiAcademyTrustID.Should().Be(123);
        result.Name.Should().Be("Test MAT");
        result.AcademyCanReviewEvidence.Should().BeTrue();
    }

    [Test]
    public async Task GetMultiAcademyTrustById_WhenMatDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.GetMultiAcademyTrustById(999);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateAcademyCanReviewEvidence_WhenMatExists_UpdatesFlagAndReturnsUpdatedMat()
    {
        // Arrange
        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = 456,
            Name = "Another MAT",
            AcademyCanReviewEvidence = false
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAcademyCanReviewEvidence(456, true);

        // Assert
        result.Should().NotBeNull();
        result!.AcademyCanReviewEvidence.Should().BeTrue();

        var savedMat = await _fakeInMemoryDb.MultiAcademyTrusts
            .FirstOrDefaultAsync(x => x.MultiAcademyTrustID == 456);

        savedMat.Should().NotBeNull();
        savedMat!.AcademyCanReviewEvidence.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAcademyCanReviewEvidence_WhenMatDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.UpdateAcademyCanReviewEvidence(999, true);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateAcademyCanReviewEvidence_WhenSettingFalse_PersistsFalse()
    {
        // Arrange
        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = 789,
            Name = "False MAT",
            AcademyCanReviewEvidence = true
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAcademyCanReviewEvidence(789, false);

        // Assert
        result.Should().NotBeNull();
        result!.AcademyCanReviewEvidence.Should().BeFalse();

        var savedMat = await _fakeInMemoryDb.MultiAcademyTrusts
            .FirstOrDefaultAsync(x => x.MultiAcademyTrustID == 789);

        savedMat.Should().NotBeNull();
        savedMat!.AcademyCanReviewEvidence.Should().BeFalse();
    }

    [Test]
    public async Task GetEstablishmentsByMultiAcademyTrustId_WhenMatExists_ReturnsEstablishments()
    {
        // Arrange
        var matId = 100;

        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = matId,
            Name = "Test MAT"
        };

        var establishment = new Establishment
        {
            EstablishmentID = 1,
            EstablishmentName = "Test School",
            County = "Test County",
            Locality = "Test Locality",
            Postcode = "AB1 2CD",
            Street = "123 Test Street",
            Town = "Test Town",
            Type = "School"

        };

        var matLink = new MultiAcademyTrustEstablishment
        {
            MultiAcademyTrustID = matId,
            EstablishmentID = establishment.EstablishmentID,
            Establishment = establishment
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);
        _fakeInMemoryDb.Establishments.Add(establishment);
        _fakeInMemoryDb.MultiAcademyTrustEstablishments.Add(matLink);

        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetEstablishmentsByMultiAcademyTrustId(matId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);

        result[0].URN.Should().Be(establishment.EstablishmentID);
        result[0].Name.Should().Be("Test School");
    }

    [Test]
    public async Task GetEstablishmentsByMultiAcademyTrustId_WhenMatExistsButNoEstablishments_ReturnsEmptyList()
    {
        // Arrange
        var matId = 200;

        var mat = new MultiAcademyTrust
        {
            MultiAcademyTrustID = matId,
            Name = "Empty MAT"
        };

        _fakeInMemoryDb.MultiAcademyTrusts.Add(mat);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetEstablishmentsByMultiAcademyTrustId(matId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Test]
    public void GetEstablishmentsByMultiAcademyTrustId_WhenMatDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var invalidMatId = 999;

        // Act
        Func<Task> act = async () =>
            await _sut.GetEstablishmentsByMultiAcademyTrustId(invalidMatId);

        // Assert
        act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Unable to find establishments: - {invalidMatId}*");
    }
}