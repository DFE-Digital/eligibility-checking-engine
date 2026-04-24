using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Tests;

public class MultiAcademyTrustGatewayTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private MultiAcademyTrustGateway _sut;

    [SetUp]
    public async Task Setup()
    {
        var databaseName = $"FakeInMemoryDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var context = (EligibilityCheckContext)_fakeInMemoryDb;
        await context.Database.EnsureCreatedAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        _sut = new MultiAcademyTrustGateway(_fakeInMemoryDb);
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
}