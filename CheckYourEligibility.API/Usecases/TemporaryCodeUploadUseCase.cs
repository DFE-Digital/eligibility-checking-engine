using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FeatureManagement.Domain.Validation;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases
{
	public interface ITemporaryCodeUploadUseCase
	{
		Task<TemporaryCodeUploadCreateResponse> Execute(TemporaryCodeUploadCreateRequest request, string userId);
	}

	public class TemporaryCodeUploadUseCase : ITemporaryCodeUploadUseCase
	{
		private readonly ITemporaryCodeUpload _gateway;
		private readonly IAdministration _administrationGateway;
		private readonly IWorkingFamiliesEvent _workingFamiliesEventGateway;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<TemporaryCodeUploadUseCase> _logger;

		private readonly List<ImportTempCodeValidationError> validationErrors = new();

		private readonly IWorkingFamiliesReporting _workingFamiliesReportingGateway;

		public TemporaryCodeUploadUseCase(
			IWorkingFamiliesReporting workingFamiliesReportingGateway)
		{
			_workingFamiliesReportingGateway = workingFamiliesReportingGateway;
		}

		public TemporaryCodeUploadUseCase(
		ITemporaryCodeUpload gateway,
		IAdministration administrationGateway,
		IWorkingFamiliesEvent workingFamiliesEventGateway,
		IWorkingFamiliesReporting workingFamiliesReportingGateway,
		IServiceScopeFactory scopeFactory,
		ILogger<TemporaryCodeUploadUseCase> logger)
		{
			_gateway = gateway;
			_administrationGateway = administrationGateway;
			_workingFamiliesReportingGateway = workingFamiliesReportingGateway;
			_workingFamiliesEventGateway = workingFamiliesEventGateway;
			_scopeFactory = scopeFactory;
			_logger = logger;
		}

		public async Task<TemporaryCodeUploadCreateResponse> Execute(TemporaryCodeUploadCreateRequest request, string userId)
		{
			List<WorkingFamiliesEvent> DataLoad = new();
			if (request.File == null || (request.File.ContentType.ToLower() != "text/xml" && !request.File.FileName.EndsWith(".xlsm")))
				throw new InvalidDataException($"{Admin.XlsmfileRequired}");

			var validator = new WorkingFamiliesEventImportValidator();

			// Validate file content, parse dataload to WorkingFamilyEvent object and import new events to table
			try
			{
				using var fileStream = request.File.OpenReadStream();
				SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(fileStream, false);
				WorkbookPart workbookPart = spreadsheetDocument.WorkbookPart;
				WorksheetPart worksheetPart = workbookPart.WorksheetParts.First();
				SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
				var cellStyles = workbookPart.WorkbookStylesPart.Stylesheet.CellFormats.Elements<CellFormat>().ToArray();
				var sharedStrings = workbookPart.GetPartsOfType<SharedStringTablePart>().First().SharedStringTable;

				var headerRow = sheetData.Elements<Row>().ElementAt(0);
				var columnHeaders = CsvGetHelper.GetColumnHeaders(headerRow, sharedStrings);
				var eventRows = from row in headerRow.ElementsAfter()
								where row.Elements<Cell>().ElementAt(1).CellValue is not null
								select row;
				foreach (Row row in eventRows)
				{
					var nino = row.Elements<Cell>().ElementAt(1);
					var ninoValueString = CsvGetHelper.getCellValueString(nino, sharedStrings, cellStyles);
					if (string.IsNullOrEmpty(ninoValueString))
					{
						continue;
					}
					List<string> eventProps = [];
					foreach (Cell cell in row.Elements<Cell>().Skip(1))
					{
						var cellValueString = CsvGetHelper.getCellValueString(cell, sharedStrings, cellStyles);
						eventProps.Add(cellValueString);
					}

					var wfEvent = WorkingFamiliesEventHelper.ParseWorkingFamiliesEvent(eventProps, columnHeaders);
					var validationResults = validator.Validate(wfEvent);
					//if wfEvent has a valid eligibility code then check wfevents table to confirm eligibility code does not already exist
					bool hasDuplicateEligibilityCode = false;
					if (!string.IsNullOrWhiteSpace(wfEvent.EligibilityCode))
					{
						if (!validationResults.Errors.Any(x => x.ErrorMessage.Contains("eligibility", StringComparison.OrdinalIgnoreCase)))
						{
							//compare eligibility code to wfevents table to check for duplication.
							var result = await _workingFamiliesReportingGateway.GetAllWorkingFamiliesEventsByEligibilityCode(wfEvent.EligibilityCode);
							if (result.Data.Any())
							{
								if (validationResults.Errors.Count > 0)
								{
									validationResults.Errors.Insert(0, new FluentValidation.Results.ValidationFailure()
									{
										AttemptedValue = wfEvent.EligibilityCode,
										ErrorMessage = ValidationMessages.EligibilityCodeAlreadyExists,
										PropertyName = nameof(wfEvent.EligibilityCode),
									});
								}
								else
								{
									validationErrors.Add(new ImportTempCodeValidationError
									{
										RowNumber = Convert.ToInt32(row.RowIndex.Value),
										EligibilityCode = wfEvent.EligibilityCode,
										ErrorMessages = new List<string> { ValidationMessages.EligibilityCodeAlreadyExists }
									});
								}
							}
						}
					}
					if (!validationResults.IsValid)
					{
						validationErrors.Add(new ImportTempCodeValidationError
						{
							RowNumber = Convert.ToInt32(row.RowIndex.Value),
							EligibilityCode = wfEvent.EligibilityCode,
							ErrorMessages = validationResults.Errors
								.Select(x => x.ErrorMessage)
								.ToList()
						});
						continue;
					}
					DataLoad.Add(wfEvent);
				}
				if (validationErrors.Any())
				{
					throw new BulkTempCodeValidationException(validationErrors);
				}
				if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file no content.");
				await _administrationGateway.BulkImportWorkingFamiliesEventHMRCData(DataLoad);
			}
			catch (BulkTempCodeValidationException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError("ImportWfHMRCData", ex);

				if (!validationErrors.Any())
				{
					validationErrors.Add(
						new ImportTempCodeValidationError
						{
							RowNumber = 0,
							EligibilityCode = string.Empty,
							ErrorMessages = new List<string>
							{
								ex.Message
							}
						});
				}

				throw new BulkTempCodeValidationException(validationErrors);
			}
			//Run business logic and upsert summary record for each event
			try
			{
				IList<WorkingFamiliesEventSummary> summaryRecordsDataLoad = [];

				for (int i = 0; i < DataLoad.Count; i++)
				{
					WorkingFamiliesEventSummary eventSummaryRecord = new();

					// check for existing records in the working families events table
					// check for existing summary record for that event
					var summaryRecord = await _workingFamiliesEventGateway.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(DataLoad[i].EligibilityCode);
					int historicEventRecordsCount = await _workingFamiliesEventGateway.GetWorkingFamiliesEventsCount(DataLoad[i].EligibilityCode);
					// pass record to evaluate contiguity for each incoming event
					eventSummaryRecord = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(DataLoad[i], summaryRecord, historicEventRecordsCount);
					summaryRecordsDataLoad.Add(eventSummaryRecord);


				}
				await _workingFamiliesEventGateway.BulkImportWorkingFamiliesEventSummaryRecords(summaryRecordsDataLoad);
			}
			catch (Exception ex)
			{
				_logger.LogError("ImportWfHMRCData", ex);
				throw;
			}

			var numberOfCodes = DataLoad.Count;
			var firstCode = DataLoad.First().EligibilityCode;
			var lastCode = DataLoad.Last().EligibilityCode;

			var response = await _gateway.CreateTemporaryCodeUpload(request, userId, numberOfCodes, firstCode, lastCode);

			return new TemporaryCodeUploadCreateResponse
			{
				NumberOfCodes = response.NumberOfCodes,
				Errors = new List<ImportTempCodeValidationError>()
			};
		}

	}
}
