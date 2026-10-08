using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
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
    public async Task Execute_Should_Accept_XML_File_Type_But_Reject_Invalid_Content()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("test.xml");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(() => new MemoryStream());

        Func<Task> act = () => _sut.Execute(fileMock.Object);

        var exception = await act.Should()
            .ThrowExactlyAsync<InvalidDataException>();

        exception.Which.Message.Should().Be(
            "Invalid file content. Check the file format and values.");

        fileMock.Verify(f => f.OpenReadStream(), Times.Once);

        _mockGateway.Verify(
            g => g.BulkImportWorkingFamiliesEventHMRCData(
                It.IsAny<IEnumerable<WorkingFamiliesEvent>>()),
            Times.Never);
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
           .ReturnsAsync(new WorkingFamiliesEventSummary
           {
               EligibilityCode = "50173110191",
               ValidityStartDate = new DateTime(2000, 1, 1),
               DiscretionaryValidityStartDate = new DateTime(2000, 1, 1),
               ValidityEndDate = new DateTime(2000, 3, 31),
               GracePeriodEndDate = new DateTime(2000,8,31)
           });

        _mockWorkingFamiliesEventGateway.Setup(s => s.GetWorkingFamiliesEventsCount("50173110191")).ReturnsAsync(1);
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
            Times.Exactly(1));
    }

    [Test]
    public async Task Execute_InvalidData_Should_Throw_Validation_Exception()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName)
            .Returns("HMRCManualEligibilityEvent_invalid.xlsm");

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent_invalid.xlsm");

        stream.Should().NotBeNull();
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream!);

        const string expectedMessage =
            "On row 2: Eligibility code must be 11 digits long, Invalid National Insurance Number, Submission date must not be in the future";

        Func<Task> act = () => _sut.Execute(fileMock.Object);

        var exception = await act.Should()
            .ThrowExactlyAsync<InvalidDataException>();

        exception.Which.Message.Should().Be(expectedMessage);

        _mockGateway.Verify(
            g => g.BulkImportWorkingFamiliesEventHMRCData(
                It.IsAny<IEnumerable<WorkingFamiliesEvent>>()),
            Times.Never);
    }

    [Test]
    public async Task Execute_Should_Throw_InvalidDataException_When_Xlsm_File_Has_No_Content()
    {
        using var resource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(
                "CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent.xlsm");

        resource.Should().NotBeNull();

        using var editableWorkbook = new MemoryStream();
        resource!.CopyTo(editableWorkbook);
        editableWorkbook.Position = 0;

        using (var document = SpreadsheetDocument.Open(editableWorkbook, true))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.First();
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            // Preserve the headers, removing every event row.
            foreach (var row in sheetData.Elements<Row>().Skip(1).ToList())
            {
                row.Remove();
            }

            worksheetPart.Worksheet.Save();
        }

        var workbookBytes = editableWorkbook.ToArray();

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("empty.xlsm");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(() => new MemoryStream(workbookBytes));

        Func<Task> act = () => _sut.Execute(fileMock.Object);

        var exception = await act.Should()
            .ThrowExactlyAsync<InvalidDataException>();

        exception.Which.Message.Should().Be("Invalid file no content.");

        _mockGateway.Verify(
            g => g.BulkImportWorkingFamiliesEventHMRCData(
                It.IsAny<IEnumerable<WorkingFamiliesEvent>>()),
            Times.Never);
    }

    [Test]
    public async Task Execute_Should_Not_Expose_Source_Error_Details()
    {
        const string privateValue = "PRIVATE-IMPORT-VALUE-3644";

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/xml");
        fileMock.Setup(f => f.FileName).Returns("import.xlsm");
        fileMock.Setup(f => f.OpenReadStream())
            .Throws(new InvalidDataException(
                $"Source error containing {privateValue}"));

        Func<Task> act = () => _sut.Execute(fileMock.Object);

        var exception = await act.Should()
            .ThrowExactlyAsync<InvalidDataException>();

        exception.Which.Message.Should().NotContain(privateValue);

        exception.Which.Message.Should().Be(
            "Invalid file content. Check the file format and values.");
        exception.Which.ToString().Should().NotContain(privateValue);
        exception.Which.InnerException.Should().BeNull();

        var logCalls = _mockLogger.Invocations
            .Where(invocation => invocation.Method.Name == "Log")
            .ToList();

        logCalls.Should().ContainSingle();

        var logCall = logCalls.Single();
        logCall.Arguments[0].Should().Be(LogLevel.Error);
        logCall.Arguments[3].Should().BeNull(
            "the original exception may contain submitted personal data");

        var logState =
            (IEnumerable<KeyValuePair<string, object>>)logCall.Arguments[2];

        foreach (var entry in logState)
        {
            (entry.Value?.ToString() ?? string.Empty)
                .Should().NotContain(privateValue);
        }

        logCall.Arguments[2].ToString().Should().Be(
            "Working Families import failed. Error type: InvalidDataException");

        _mockGateway.Verify(
            g => g.BulkImportWorkingFamiliesEventHMRCData(
                It.IsAny<IEnumerable<WorkingFamiliesEvent>>()),
            Times.Never);
    }

}