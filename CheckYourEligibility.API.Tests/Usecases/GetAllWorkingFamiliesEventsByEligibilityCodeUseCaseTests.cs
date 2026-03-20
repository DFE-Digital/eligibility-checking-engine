using AutoFixture;
using Castle.Core.Logging;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetAllWorkingFamiliesEventsByEligibilityCodeUseCaseTests : TestBase.TestBase
{
    private Mock<IWorkingFamiliesReporting> _mockWorkingFamiliesReportingGateway;
    private Mock<ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase>> _mockLogger;
    private GetAllWorkingFamiliesEventsByEligibilityCodeUseCase _sut;
    private Fixture _fixture = null!;

    [SetUp]
    public void Setup()
    {
        _mockWorkingFamiliesReportingGateway = new Mock<IWorkingFamiliesReporting>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase>>(MockBehavior.Loose);
        _sut = new GetAllWorkingFamiliesEventsByEligibilityCodeUseCase(_mockWorkingFamiliesReportingGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockWorkingFamiliesReportingGateway.VerifyAll();
    }


    [Test]
    public async Task Execute_returns_success_with_correct_wf_blocks()
    {
        // arrange
        var eligibilityCode = "ABC12345678";
        var localAuthorityIds = new List<int> { 123, 456 };

        // single app
        var wfResponse = new WorkingFamilyEventByEligibilityCodeRepsonse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
        {
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEvent
                {
                    WorkingFamiliesEventID = "E1",
                    EligibilityCode = eligibilityCode,
                    SubmissionDate = DateTime.UtcNow
                }
            }
        }
        };

        _mockWorkingFamiliesReportingGateway
            .Setup(g => g.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode))
            .ReturnsAsync(wfResponse);

        // act
        var result = await _sut.Execute(eligibilityCode);

        // assert
        result.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        result.Data.First().Record.WorkingFamiliesEventID.Should().Be("E1");

        _mockWorkingFamiliesReportingGateway.Verify(
            g => g.GetAllWorkingFamiliesEventsByEligibilityCode(eligibilityCode),
            Times.Once);
    }


    [Test]
    public async Task Execute_returns_failure_when_eligibilityCode_is_null_or_empty()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(null);
        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, Eligibility Code is required.");
    }


}