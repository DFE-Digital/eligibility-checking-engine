using System.Text;
using AutoFixture;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Tests.Properties;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ImportWfHMRCDataUseCaseTests : TestBase.TestBase
{
    private Mock<IAdministration> _mockGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<ImportWfHMRCDataUseCase>> _mockLogger;
    private ImportWfHMRCDataUseCase _sut;
    private Fixture _fixture;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IAdministration>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ImportWfHMRCDataUseCase>>(MockBehavior.Loose);
        _sut = new ImportWfHMRCDataUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
    {
        // Arrange
        IFormFile file = null;

        // Act
        var act = async () => await _sut.Execute(file);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>().WithMessage("xlsm file required.");
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Not_Xlsm()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/plain");

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>().WithMessage("xlsm file required.");
    }

    [Test]
    public async Task Execute_Should_Process_Xlsm_File_And_Call_ImportWfHMRCData()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xlsm");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(Resources.exampleWfHMRC)));

        _mockGateway.Setup(s => s.ImportWfHMRCData(It.IsAny<List<WorkingFamiliesEvent>>())).Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        //TODO: Reduce the size od the test data file for API tests
        _mockGateway.Verify(
            s => s.ImportWfHMRCData(It.Is<List<WorkingFamiliesEvent>>(list =>
                list.Count == 2 && list[0].EligibilityCode == "50173110190" &&
                list[1].WorkingFamiliesEventId == "50173110191")), Times.Once);
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_Xlsm_File_Has_No_Content()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xlsm");
        fileMock.Setup(f => f.FileName).Returns("test.xlsm");

        // Create Xlsm without events
        //TODO: Set up file mock to return an empty xlsm stream

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("Invalid file no content.");
    }

}