using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using System.Reflection;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ImportWfHMRCDataUseCaseTests : TestBase.TestBase
{
    private Mock<IAdministration> _mockGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<ImportWfHMRCDataUseCase>> _mockLogger;
    private Mock<IWorkingFamiliesEvent> _mockWorkingFamiliesEventGateway;
    private ImportWfHMRCDataUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IAdministration>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ImportWfHMRCDataUseCase>>(MockBehavior.Loose);
        _mockWorkingFamiliesEventGateway = new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);

        _sut = new ImportWfHMRCDataUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockWorkingFamiliesEventGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
        _mockWorkingFamiliesEventGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
    {
        // Arrange
        IFormFile file = null;

        // Act
        var act = async () => await _sut.Execute(file);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>().Result.WithMessage("Xlsm data file is required.");
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Not_Xlsm()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/plain");
        fileMock.Setup(f => f.FileName).Returns("test.txt");

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>().Result.WithMessage("Xlsm data file is required.");
    }

    [Test]
    public void Execute_Should_Accept_XML_File()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("test.xml");
        // This will fail later due to invalid XML content, but should pass the file type validation
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert - should not throw InvalidDataException for file type, but will throw for invalid content
        act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("Invalid file no content.");
    }

    [Test]
    public async Task Execute_Should_Process_Xlsm_File_And_Call_BulkImportWorkingFamiliesEventHMRCData_BulkImportWorkingFamiliesEventSummaryRecords()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("HMRCManualEligibilityEvent.xlsm");
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent.xlsm");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(stream);

        _mockWorkingFamiliesEventGateway
            .Setup(s => s.GetWorkingFamiliesEventSummaryRecordByEligibilityCode("50173110190"))
            .ReturnsAsync((WorkingFamiliesEventSummary?)null);
        _mockWorkingFamiliesEventGateway
            .Setup(s => s.GetWorkingFamiliesEventSummaryRecordByEligibilityCode("50173110191"))
            .ReturnsAsync((WorkingFamiliesEventSummary?)null);
        _mockWorkingFamiliesEventGateway
            .Setup(s => s.GetWorkingFamiliesEventsCount("50173110190"))
            .ReturnsAsync(0);
        _mockWorkingFamiliesEventGateway
            .Setup(s => s.GetWorkingFamiliesEventsCount("50173110191"))
            .ReturnsAsync(0);

        _mockGateway.Setup(s => s.BulkImportWorkingFamiliesEventHMRCData(It.IsAny<List<WorkingFamiliesEvent>>())).Returns(Task.CompletedTask);
        _mockWorkingFamiliesEventGateway.Setup(s=> s.BulkImportWorkingFamiliesEventSummaryRecords(It.IsAny<List<WorkingFamiliesEventSummary>>())).Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        _mockGateway.Verify(
            s => s.BulkImportWorkingFamiliesEventHMRCData(
                It.Is<List<WorkingFamiliesEvent>>(list =>
                    list.Count == 2
                    && list[0].EligibilityCode == "50173110190"
                    && list[1].EligibilityCode == "50173110191")),
            Times.Once);
        _mockWorkingFamiliesEventGateway.Verify(
           s => s.BulkImportWorkingFamiliesEventSummaryRecords(
               It.Is<List<WorkingFamiliesEventSummary>>(list =>
                   list.Count == 2
                   && list[0].EligibilityCode == "50173110190"
                   && list[1].EligibilityCode == "50173110191")),
           Times.Once);
        _mockWorkingFamiliesEventGateway.Verify(
            s => s.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(It.IsAny<string>()),
            Times.Exactly(2));
        _mockWorkingFamiliesEventGateway.Verify(
            s => s.GetWorkingFamiliesEventsCount(It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Test]
    public void Execute_InvalidData_Should_Throw_Validation_Exception()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("HMRCManualEligibilityEvent_invalid.xlsm");
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent_invalid.xlsm");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(stream);
        string validationMessage = "On row 2: Eligibility code must be 11 digits long, Invalid National Insurance Number, Submission date must not be in the future";
        string exceptionMessage = $"HMRCManualEligibilityEvent_invalid.xlsm - {JsonConvert.SerializeObject(new WorkingFamiliesEvent())} :- {validationMessage}, ";
        
        // Act
        Func<Task> act = async () => await _sut.Execute(fileMock.Object);

        act.Should().ThrowExactlyAsync<InvalidDataException>().Result.WithMessage(exceptionMessage);
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_Xlsm_File_Has_No_Content()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");

        // Create Xlsm without events
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("Invalid file no content.");
    }

}