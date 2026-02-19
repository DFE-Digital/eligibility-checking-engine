using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEligibilityCheckItemUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetEligibilityCheckItemUseCase>>(MockBehavior.Loose);
        _sut = new GetEligibilityCheckItemUseCase(_mockCheckGateway.Object, _mockAuditGateway.Object,
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
    private Mock<ILogger<GetEligibilityCheckItemUseCase>> _mockLogger;
    private GetEligibilityCheckItemUseCase _sut;
    private Fixture _fixture;

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Execute_returns_failure_when_guid_is_null_or_empty(string guid)
    {
        // Arrange
        var type = _fixture.Create<CheckEligibilityType>();

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, type);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, check ID is required.");
    }

    [Test]
    public async Task Execute_returns_notFound_when_gateway_returns_null()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        _mockCheckGateway.Setup(s => s.GetItem<CheckEligibilityItem>(guid, type, false))
            .ReturnsAsync((CheckEligibilityItem)null);

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, type);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage($"Bulk upload with ID {guid} not found");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_and_links_when_gateway_returns_item()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = CheckEligibilityType.None;
        var item = _fixture.Create<CheckEligibilityItem>();
        _mockCheckGateway.Setup(s => s.GetItem<CheckEligibilityItem>(guid, type, false)).ReturnsAsync(item);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid, null)).ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, type);

        // Assert
        result.Data.Should().Be(item);
        result.Links.Should().NotBeNull();
        result.Links.Get_EligibilityCheck.Should().Be($"{CheckLinks.GetLink}{guid}");
        result.Links.Put_EligibilityCheckProcess.Should().Be($"{CheckLinks.ProcessLink}{guid}");
        result.Links.Get_EligibilityCheckStatus.Should().Be($"{CheckLinks.GetLink}{guid}/Status");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_and_links_when_gateway_returns_item_with_type()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = CheckEligibilityType.FreeSchoolMeals;
        var item = _fixture.Create<CheckEligibilityItem>();
        _mockCheckGateway.Setup(s => s.GetItem<CheckEligibilityItem>(guid, type, false)).ReturnsAsync(item);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid, null)).ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(guid, type);

        // Assert
        result.Data.Should().Be(item);
        result.Links.Should().NotBeNull();
        result.Links.Get_EligibilityCheck.Should().Be($"{CheckLinks.GetLink}{type}/{guid}");
        result.Links.Put_EligibilityCheckProcess.Should().Be($"{CheckLinks.ProcessLink}{guid}");
        result.Links.Get_EligibilityCheckStatus.Should().Be($"{CheckLinks.GetLink}{type}/{guid}/Status");
    }

    [Test]
    public async Task Execute_calls_gateway_GetItem_with_correct_guid()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var type = _fixture.Create<CheckEligibilityType>();
        var item = _fixture.Create<CheckEligibilityItem>();
        _mockCheckGateway.Setup(s => s.GetItem<CheckEligibilityItem>(guid, type, false)).ReturnsAsync(item);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, guid, null)).ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, type);

        // Assert
        _mockCheckGateway.Verify(s => s.GetItem<CheckEligibilityItem>(guid, type, false), Times.Once);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Check, guid, null), Times.Once);
    }
}