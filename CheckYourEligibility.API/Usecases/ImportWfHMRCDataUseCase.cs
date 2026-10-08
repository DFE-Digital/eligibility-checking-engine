using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FeatureManagement.Domain.Validation;
using FluentValidation;

namespace CheckYourEligibility.API.UseCases;

public interface IImportWfHMRCDataUseCase
{
    Task Execute(IFormFile file);
}

public class ImportWfHMRCDataUseCase : IImportWfHMRCDataUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly IWorkingFamiliesEvent _workingFamiliesEventGateway;
    private readonly ILogger<ImportWfHMRCDataUseCase> _logger;

    public ImportWfHMRCDataUseCase(IAdministration Gateway, IAudit auditGateway,IWorkingFamiliesEvent workingFamiliesEventGateway,
        ILogger<ImportWfHMRCDataUseCase> logger)
    {
        _gateway = Gateway;
        _workingFamiliesEventGateway = workingFamiliesEventGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<WorkingFamiliesEvent> DataLoad = new();
        if (file == null || (file.ContentType.ToLower() != "text/xml" && !file.FileName.EndsWith(".xlsm")))
            throw new InvalidDataException($"{Admin.XlsmfileRequired}");

        var validator = new WorkingFamiliesEventImportValidator();
        var safeErrorMessage = "Invalid file content. Check the file format and values.";


        // Validate file content, parse dataload to WorkingFamilyEvent object and import new events to table
        try
        {
            using var fileStream = file.OpenReadStream();
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
                List<string> eventProps = [];
                foreach (Cell cell in row.Elements<Cell>().Skip(1))
                {
                    try
                    {
                        CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
                        var cellValueString = CsvGetHelper.getCellValueString(cell, sharedStrings, cellStyles);
                        eventProps.Add(cellValueString);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Failed to parse data at {cell.CellReference}:- {ex.Message}");
                    }
                }

                var wfEvent = WorkingFamiliesEventHelper.ParseWorkingFamiliesEvent(eventProps, columnHeaders);
                var validationResults = validator.Validate(wfEvent);
                if (!validationResults.IsValid)
                {
                    safeErrorMessage =
                        $"On row {row.RowIndex}: {validationResults.ToString().ReplaceLineEndings(", ")}";
                    throw new ValidationException(safeErrorMessage);
                }

                wfEvent.ParentNationalInsuranceNumber =
                    NinoValidation.Normalize(wfEvent.ParentNationalInsuranceNumber);
                DataLoad.Add(wfEvent);
            }
if (DataLoad.Count == 0)
{
    safeErrorMessage = "Invalid file no content.";
    throw new InvalidDataException(safeErrorMessage);
}

await _gateway.BulkImportWorkingFamiliesEventHMRCData(DataLoad);
}
catch (Exception ex)
{
    _logger.LogError(
        "Working Families import failed. Error type: {ErrorType}",
        ex.GetType().Name);

    throw new InvalidDataException(safeErrorMessage);
}

// Run business logic and upsert summary record for each event
try
{
    IList<WorkingFamiliesEventSummary> summaryRecordsDataLoad = [];

    for (int i = 0; i < DataLoad.Count; i++)
    {
        WorkingFamiliesEventSummary eventSummaryRecord = new();

        // Check for existing records in the working families events table
        // Check for existing summary record for that event
        var summaryRecord = await _workingFamiliesEventGateway.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(DataLoad[i].EligibilityCode);

        int historicEventRecordsCount = await _workingFamiliesEventGateway.GetWorkingFamiliesEventsCount(DataLoad[i].EligibilityCode);

        // Pass record to evaluate contiguity for each incoming event
        eventSummaryRecord = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(
            DataLoad[i], summaryRecord, historicEventRecordsCount);

        summaryRecordsDataLoad.Add(eventSummaryRecord);
    }

    await _workingFamiliesEventGateway
        .BulkImportWorkingFamiliesEventSummaryRecords(summaryRecordsDataLoad);
}
catch (Exception ex)
{
    _logger.LogError("ImportWfHMRCData", ex);
    throw;
}
    }

}