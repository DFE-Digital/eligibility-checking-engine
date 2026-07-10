using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
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
using Newtonsoft.Json;
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
            .ReturnsAsync(new CheckYourEligibility.API.Domain.BulkCheck
            {
                BulkCheckID = bulkCheckId,
                Status = BulkCheckStatus.Completed
            });

        _mockCreateApplicationsFromBulkCheckGateway
            .Setup(x => x.GetEligibleChecks(bulkCheckId))
            .ReturnsAsync(new List<EligibilityCheck>());

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

    [Test]
    public async Task ProcessApplicationsFromBulkCheck_maps_EmailAddress_to_ParentEmail()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        var allowedLocalAuthorityIds = new List<int> { 0 };

        var eligibleChecks = new List<EligibilityCheck>
        {
            new()
            {
                EligibilityCheckID = Guid.NewGuid().ToString(),
                CheckData = JsonConvert.SerializeObject(new CheckProcessData
                {
                    FirstName = "Parent",
                    LastName = "Tester",
                    DateOfBirth = "1980-01-01",
                    NationalInsuranceNumber = "NA123456C",
                    ChildFirstName = "Child",
                    ChildLastName = "Tester",
                    ChildDateOfBirth = "2015-01-01",
                    ChildSchoolURN = "123456",
                    EmailAddress = "parent@example.com"
                })
            }
        };

        ApplicationRequest? capturedRequest = null;

        _mockCreateApplicationUseCase
            .Setup(x => x.Execute(It.IsAny<ApplicationRequest>(), allowedLocalAuthorityIds))
            .Callback<ApplicationRequest, List<int>>((request, _) => capturedRequest = request)
            .ReturnsAsync(new ApplicationSaveItemResponse());

        // Act
        await _sut.ProcessApplicationsFromBulkCheck(
            bulkCheckId,
            eligibleChecks,
            allowedLocalAuthorityIds);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Data.ParentEmail.Should().Be("parent@example.com");

        _mockCreateApplicationsFromBulkCheckGateway.Verify(
            x => x.UpdateBulkCheckStatus(bulkCheckId, BulkCheckStatus.ApplicationsCreated),
            Times.Once);
    }

    [Test]
    public async Task ProcessApplicationsFromBulkCheck_sets_ApplicationCreationFailed_when_FirstName_is_missing()
    {
        // Arrange
        var bulkCheckId = Guid.NewGuid().ToString();
        var allowedLocalAuthorityIds = new List<int> { 0 };

        var eligibleChecks = new List<EligibilityCheck>
        {
            new()
            {
                EligibilityCheckID = Guid.NewGuid().ToString(),
                CheckData = JsonConvert.SerializeObject(new CheckProcessData
                {
                    FirstName = null,
                    LastName = "Tester",
                    DateOfBirth = "1980-01-01",
                    NationalInsuranceNumber = "NA123456C",
                    ChildFirstName = "Child",
                    ChildLastName = "Tester",
                    ChildDateOfBirth = "2015-01-01",
                    ChildSchoolURN = "123456"
                })
            }
        };

        // Act
        await _sut.ProcessApplicationsFromBulkCheck(
            bulkCheckId,
            eligibleChecks,
            allowedLocalAuthorityIds);

        // Assert
        _mockCreateApplicationUseCase.Verify(
            x => x.Execute(
                It.IsAny<ApplicationRequest>(),
                It.IsAny<List<int>>()),
            Times.Never);

        _mockCreateApplicationsFromBulkCheckGateway.Verify(
            x => x.UpdateBulkCheckStatus(
                bulkCheckId,
                BulkCheckStatus.ApplicationCreationFailed),
            Times.Once);
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

