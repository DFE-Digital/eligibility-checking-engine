using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateEligibilityCheckStatusUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<UpdateEligibilityCheckStatusUseCase>>(MockBehavior.Loose);
        _sut = new UpdateEligibilityCheckStatusUseCase(_mockCheckGateway.Object, _mockAuditGateway.Object,
            _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<ICheckEligibility> _mockCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<UpdateEligibilityCheckStatusUseCase>> _mockLogger;
    private UpdateEligibilityCheckStatusUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Arrange
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, request);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, check ID is required.");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_is_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, null);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, update data is required.");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = new EligibilityStatusUpdateRequest { Data = null };

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, request);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, update data is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();

        _mockCheckGateway
            .Setup(s => s.UpdateEligibilityCheckStatus(guid, request.Data, CheckEligibilityType.None))
            .ReturnsAsync((CheckEligibilityStatusResponse)null);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, request);

        // Assert
        act.Should().ThrowAsync<NotFoundException>().WithMessage($"Bulk upload with ID {guid} not found");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_status()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();
        var responseData = _fixture.Create<CheckEligibilityStatusResponse>();

        _mockCheckGateway
            .Setup(s => s.UpdateEligibilityCheckStatus(guid, request.Data, CheckEligibilityType.None))
            .ReturnsAsync(responseData);


        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid)).ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, request);

        // Assert
        result.Data.Should().Be(responseData.Data);
    }

    [Test]
    public async Task Execute_calls_gateway_UpdateEligibilityCheckStatus_with_correct_parameters()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var request = _fixture.Create<EligibilityStatusUpdateRequest>();
        var responseData = _fixture.Create<CheckEligibilityStatusResponse>();

        _mockCheckGateway
            .Setup(s => s.UpdateEligibilityCheckStatus(guid, request.Data, CheckEligibilityType.None))
            .ReturnsAsync(responseData);

        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid)).ReturnsAsync(_fixture.Create<string>());


        // Act
        await _sut.Execute(guid, request);

        // Assert
        _mockCheckGateway.Verify(s => s.UpdateEligibilityCheckStatus(guid, request.Data, CheckEligibilityType.None), Times.Once);
    }
}