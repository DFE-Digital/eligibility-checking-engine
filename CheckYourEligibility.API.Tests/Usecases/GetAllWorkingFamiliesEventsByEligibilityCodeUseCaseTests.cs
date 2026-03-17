using AutoFixture;
using Castle.Core.Logging;
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
    private Mock<IAudit> _mockAuditGateway = null!;
    private GetAllWorkingFamiliesEventsByEligibilityCodeUseCase _sut;
    private Fixture _fixture = null!;

    [SetUp]
    public void Setup()
    {
        _mockWorkingFamiliesReportingGateway = new Mock<IWorkingFamiliesReporting>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetAllWorkingFamiliesEventsByEligibilityCodeUseCase>>(MockBehavior.Loose);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new GetAllWorkingFamiliesEventsByEligibilityCodeUseCase(_mockWorkingFamiliesReportingGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockWorkingFamiliesReportingGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_returns_failure_when_eligibilityCode_and_LAs_is_null()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(null, null);
        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, Eligibility Code is required.");
    }

   
}