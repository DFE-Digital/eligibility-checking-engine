using AutoFixture;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetAllWorkingFamiliesEventsByEligibilityCodeUseCaseTests : TestBase
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
        var wfResponse = new WorkingFamilyEventByEligibilityCodeResponse
        {
            Data = new List<WorkingFamilyEventByEligibilityCodeResponseItem>
        {
            new()
            {
                Event = WorkingFamilyEventType.Application,
                Record = new WorkingFamiliesEventEligibilityCodeResponseRecord
                {
                    EventId = "C3-A",
                    SubmissionDate = new DateTime(2025,08,01)
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
        result.Data.First().Record.EventId.Should().Be("C3-A");

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