using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using DomainValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;


namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateApplicationsFromBulkCheckUseCaseTests
{
    private DbContextOptions<EligibilityCheckContext> _dbContextOptions = null!;
    private IDbContextFactory<EligibilityCheckContext> _dbContextFactory = null!;
    private Mock<IServiceScopeFactory> _mockScopeFactory = null!;
    private Mock<ILogger<CreateApplicationsFromBulkCheckUseCase>> _mockLogger = null!;
    private Mock<ICreateApplicationUseCase> _mockCreateApplicationUseCase = null!;
    private CreateApplicationsFromBulkCheckUseCase _sut = null!;
    private Mock<ICreateApplicationsFromBulkCheck> _mockCreateApplicationsFromBulkCheckGateway = null!;

    [SetUp]
    public void Setup()
    {
        _dbContextOptions = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = new TestDbContextFactory(_dbContextOptions);

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<CreateApplicationsFromBulkCheckUseCase>>();
        _mockCreateApplicationUseCase = new Mock<ICreateApplicationUseCase>();
        _mockCreateApplicationsFromBulkCheckGateway = new Mock<ICreateApplicationsFromBulkCheck>();

        _sut = new CreateApplicationsFromBulkCheckUseCase(
             _mockScopeFactory.Object,
             _mockLogger.Object,
             _mockCreateApplicationUseCase.Object,
             _mockCreateApplicationsFromBulkCheckGateway.Object);
    }

    [Test]
    public async Task Execute_returns_bad_request_when_no_eligible_checks_exist_and_does_not_change_status()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();

        _mockCreateApplicationsFromBulkCheckGateway
            .Setup(x => x.GetBulkCheck(bulkCheckId))
            .ReturnsAsync(new BulkCheck
            {
                BulkCheckID = bulkCheckId,
                Status = BulkCheckStatus.Completed
            });

        _mockCreateApplicationsFromBulkCheckGateway
            .Setup(x => x.HasEligibleChecks(bulkCheckId))
            .ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await _sut.Execute(bulkCheckId, new List<int> { 0 });

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(ex => ex.Errors.Any(error => error.Title == "No eligible checks found for this bulk check"));

        _mockCreateApplicationsFromBulkCheckGateway.Verify(
            x => x.UpdateBulkCheckStatus(
                It.IsAny<string>(),
                It.IsAny<BulkCheckStatus>()),
            Times.Never);

        _mockCreateApplicationUseCase.Verify(
            x => x.Execute(It.IsAny<ApplicationRequest>(), It.IsAny<List<int>>()),
            Times.Never);
    }

    private class TestDbContextFactory : IDbContextFactory<EligibilityCheckContext>
    {
        private readonly DbContextOptions<EligibilityCheckContext> _options;

        public TestDbContextFactory(DbContextOptions<EligibilityCheckContext> options)
        {
            _options = options;
        }

        public EligibilityCheckContext CreateDbContext()
        {
            return new EligibilityCheckContext(_options);
        }
    }
}