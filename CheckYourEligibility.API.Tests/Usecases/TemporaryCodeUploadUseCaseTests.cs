using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class TemporaryCodeUploadUseCaseTests
{
	private Mock<ITemporaryCodeUpload> _temporaryCodeUploadGatewayMock = null!;
	private Mock<IAdministration> _administrationGatewayMock = null!;
	private Mock<IWorkingFamiliesEvent> _workingFamiliesEventGatewayMock = null!;
	private Mock<IWorkingFamiliesReporting> _workingFamiliesReportingGatewayMock = null!;
	private Mock<IServiceScopeFactory> _serviceScopeFactoryMock = null!;
	private Mock<ILogger<TemporaryCodeUploadUseCase>> _loggerMock = null!;

	private TemporaryCodeUploadUseCase _sut = null!;

	[SetUp]
	public void Setup()
	{
		_temporaryCodeUploadGatewayMock =
			new Mock<ITemporaryCodeUpload>(MockBehavior.Strict);

		_administrationGatewayMock =
			new Mock<IAdministration>(MockBehavior.Strict);

		_workingFamiliesEventGatewayMock =
			new Mock<IWorkingFamiliesEvent>(MockBehavior.Strict);

		_workingFamiliesReportingGatewayMock =
			new Mock<IWorkingFamiliesReporting>(MockBehavior.Strict);

		_serviceScopeFactoryMock =
			new Mock<IServiceScopeFactory>(MockBehavior.Strict);

		_loggerMock =
			new Mock<ILogger<TemporaryCodeUploadUseCase>>();

		_sut = new TemporaryCodeUploadUseCase(
			_temporaryCodeUploadGatewayMock.Object,
			_administrationGatewayMock.Object,
			_workingFamiliesEventGatewayMock.Object,
			_workingFamiliesReportingGatewayMock.Object,
			_serviceScopeFactoryMock.Object,
			_loggerMock.Object);
	}

	[TearDown]
	public void TearDown()
	{
		_temporaryCodeUploadGatewayMock.VerifyAll();
		_administrationGatewayMock.VerifyAll();
		_workingFamiliesEventGatewayMock.VerifyAll();
		_workingFamiliesReportingGatewayMock.VerifyAll();
	}

	[Test]
	public void Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
	{
		// Arrange
		var request = new TemporaryCodeUploadCreateRequest
		{
			File = null!
		};

		// Act
		Func<Task> act =
			() => _sut.Execute(request, Guid.NewGuid().ToString());

		// Assert
		act.Should()
			.ThrowExactlyAsync<InvalidDataException>();
	}

	[Test]
	public void Execute_Should_Throw_InvalidDataException_When_File_Is_Not_Xlsm()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/plain");

		fileMock.Setup(f => f.FileName)
			.Returns("test.txt");

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		// Act
		Func<Task> act =
			() => _sut.Execute(request, Guid.NewGuid().ToString());

		// Assert
		act.Should()
			.ThrowExactlyAsync<InvalidDataException>();
	}

	[Test]
	public async Task Execute_Should_Throw_BulkTempCodeValidationException_When_Eligibility_Code_Already_Exists()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		_workingFamiliesReportingGatewayMock
			.Setup(x => x.GetAllWorkingFamiliesEventsByEligibilityCode(It.IsAny<string>()))
			.ReturnsAsync(
				new WorkingFamilyEventByEligibilityCodeRepsonse
				{
					Data =
					[
						new WorkingFamilyEventByEligibilityCodeRepsonseItem()
					]
				});

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		// Act
		Func<Task> act =
			() => _sut.Execute(request, Guid.NewGuid().ToString());

		// Assert
		var exception = await act.Should()
			.ThrowExactlyAsync<BulkTempCodeValidationException>();

		exception.Which.Errors.Should().NotBeEmpty();

		exception.Which.Errors
			.SelectMany(x => x.ErrorMessages)
			.Should()
			.Contain(ValidationMessages.EligibilityCodeAlreadyExists);
	}

	[Test]
	public async Task Execute_Should_Process_File_And_Create_Temporary_Code_Upload()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		_workingFamiliesReportingGatewayMock
			.Setup(x => x.GetAllWorkingFamiliesEventsByEligibilityCode(
				It.IsAny<string>()))
			.ReturnsAsync(
				new WorkingFamilyEventByEligibilityCodeRepsonse
				{
					Data = []
				});

		_administrationGatewayMock
			.Setup(x => x.BulkImportWorkingFamiliesEventHMRCData(
				It.IsAny<List<WorkingFamiliesEvent>>()))
			.Returns(Task.CompletedTask);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(
				It.IsAny<string>()))
			.ReturnsAsync((WorkingFamiliesEventSummary?)null);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.GetWorkingFamiliesEventsCount(
				It.IsAny<string>()))
			.ReturnsAsync(0);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.BulkImportWorkingFamiliesEventSummaryRecords(
				It.IsAny<List<WorkingFamiliesEventSummary>>()))
			.Returns(Task.CompletedTask);

		_temporaryCodeUploadGatewayMock
			.Setup(x => x.CreateTemporaryCodeUpload(
				It.IsAny<TemporaryCodeUploadCreateRequest>(),
				It.IsAny<string>(),
				2,
				"50173110190",
				"50173110191"))
			.ReturnsAsync(
				new TemporaryCodeUpload
				{
					NumberOfCodes = 2
				});

		// Act
		var result = await _sut.Execute(
			request,
			Guid.NewGuid().ToString());

		// Assert
		result.Should().NotBeNull();
		result.NumberOfCodes.Should().Be(2);
		result.Errors.Should().BeEmpty();

		_workingFamiliesReportingGatewayMock.Verify(
			x => x.GetAllWorkingFamiliesEventsByEligibilityCode(It.IsAny<string>()),
			Times.Exactly(2));

		_administrationGatewayMock.Verify(
			x => x.BulkImportWorkingFamiliesEventHMRCData(
				It.IsAny<List<WorkingFamiliesEvent>>()),
			Times.Once);

		_workingFamiliesEventGatewayMock.Verify(
			x => x.BulkImportWorkingFamiliesEventSummaryRecords(
				It.IsAny<List<WorkingFamiliesEventSummary>>()),
			Times.Once);

		_temporaryCodeUploadGatewayMock.Verify(
			x => x.CreateTemporaryCodeUpload(
				It.IsAny<TemporaryCodeUploadCreateRequest>(),
				It.IsAny<string>(),
				2,
				"50173110190",
				"50173110191"),
			Times.Once);
	}

	// Regression test for ELIG-3780.
	// A QA test upload contained cells copied from Word.
	// The spreadsheet contained a cell with no StyleIndex.
	// This caused an error in: CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
	// The line has been removed as it was redundant but the test ensures future code can handle this scenario.
	[Test]
	public async Task Execute_Should_Process_File_When_Cell_Has_No_Style_Index()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent_NoStyleOnEligibilityCodes.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent_NoStyleOnEligibilityCodes.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		_workingFamiliesReportingGatewayMock
			.Setup(x => x.GetAllWorkingFamiliesEventsByEligibilityCode(
				It.IsAny<string>()))
			.ReturnsAsync(
				new WorkingFamilyEventByEligibilityCodeRepsonse
				{
					Data = []
				});

		_administrationGatewayMock
			.Setup(x => x.BulkImportWorkingFamiliesEventHMRCData(
				It.IsAny<List<WorkingFamiliesEvent>>()))
			.Returns(Task.CompletedTask);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(
				It.IsAny<string>()))
			.ReturnsAsync((WorkingFamiliesEventSummary?)null);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.GetWorkingFamiliesEventsCount(
				It.IsAny<string>()))
			.ReturnsAsync(0);

		_workingFamiliesEventGatewayMock
			.Setup(x => x.BulkImportWorkingFamiliesEventSummaryRecords(
				It.IsAny<List<WorkingFamiliesEventSummary>>()))
			.Returns(Task.CompletedTask);

		_temporaryCodeUploadGatewayMock
			.Setup(x => x.CreateTemporaryCodeUpload(
				It.IsAny<TemporaryCodeUploadCreateRequest>(),
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(new TemporaryCodeUpload
			{
				NumberOfCodes = 2
			});

		// Act
		var result = await _sut.Execute(
			request,
			Guid.NewGuid().ToString());

		// Assert
		result.Should().NotBeNull();
	}

	[Test]
	public async Task Execute_Should_Throw_BulkTempCodeValidationException_When_File_Contains_Invalid_Data()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent_invalid.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent_invalid.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		// Act
		Func<Task> act =
			() => _sut.Execute(
				request,
				Guid.NewGuid().ToString());

		// Assert
		var exception = await act.Should()
			.ThrowExactlyAsync<BulkTempCodeValidationException>();

		exception.Which.Errors.Should().NotBeEmpty();

		exception.Which.Errors.First().RowNumber.Should().Be(2);

		exception.Which.Errors
			.SelectMany(x => x.ErrorMessages)
			.Should()
			.Contain(ValidationMessages.EligibilityCodeLength);

		exception.Which.Errors
			.SelectMany(x => x.ErrorMessages)
			.Should()
			.Contain(ValidationMessages.ParentNINumberInvalid);
	}

	[Test]
	public async Task Execute_Should_Throw_BulkTempCodeValidationException_When_File_Has_No_Content()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent_empty.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent_empty.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		// Act
		Func<Task> act =
			() => _sut.Execute(
				request,
				Guid.NewGuid().ToString());

		// Assert
		var exception = await act.Should()
			.ThrowExactlyAsync<BulkTempCodeValidationException>();

		exception.Which.Errors
			.SelectMany(x => x.ErrorMessages)
			.Should()
			.Contain("Invalid file no content.");
	}

	[Test]
	public async Task Execute_Should_Throw_BulkTempCodeValidationException_When_Import_Fails()
	{
		// Arrange
		var fileMock = new Mock<IFormFile>();

		fileMock.Setup(f => f.ContentType)
			.Returns("text/xml");

		fileMock.Setup(f => f.FileName)
			.Returns("HMRCManualEligibilityEvent.xlsm");

		var stream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream(
				"CheckYourEligibility.API.Tests.Resources.HMRCManualEligibilityEvent.xlsm");

		fileMock.Setup(f => f.OpenReadStream())
			.Returns(stream);

		var request = new TemporaryCodeUploadCreateRequest
		{
			File = fileMock.Object
		};

		_workingFamiliesReportingGatewayMock
			.Setup(x => x.GetAllWorkingFamiliesEventsByEligibilityCode(
				It.IsAny<string>()))
			.ReturnsAsync(
				new WorkingFamilyEventByEligibilityCodeRepsonse
				{
					Data = []
				});

		_administrationGatewayMock
			.Setup(x => x.BulkImportWorkingFamiliesEventHMRCData(
				It.IsAny<List<WorkingFamiliesEvent>>()))
			.ThrowsAsync(new Exception("Database failure"));

		// Act
		Func<Task> act =
			() => _sut.Execute(
				request,
				Guid.NewGuid().ToString());

		// Assert
		await act.Should()
			.ThrowExactlyAsync<BulkTempCodeValidationException>();

		_temporaryCodeUploadGatewayMock.Verify(
			x => x.CreateTemporaryCodeUpload(
				It.IsAny<TemporaryCodeUploadCreateRequest>(),
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<string>(),
				It.IsAny<string>()),
			Times.Never);
	}
}
