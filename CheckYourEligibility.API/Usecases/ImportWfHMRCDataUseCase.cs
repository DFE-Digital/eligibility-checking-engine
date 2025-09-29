using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FeatureManagement.Domain.Validation;
using Newtonsoft.Json;
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
    private readonly ILogger<ImportWfHMRCDataUseCase> _logger;

    public ImportWfHMRCDataUseCase(IAdministration Gateway, IAudit auditGateway,
        ILogger<ImportWfHMRCDataUseCase> logger)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<WorkingFamiliesEvent> DataLoad = new();
        if (file == null || (file.ContentType.ToLower() != "text/xml" && !file.FileName.EndsWith(".xlsm")))
            throw new InvalidDataException($"{Admin.XlsmfileRequired}");
            
        var validator = new WorkingFamiliesEventImportValidator();
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
            var columnHeaders = GetColumnHeaders(headerRow, sharedStrings);
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
                        var cellValueString = getCellValueString(cell, sharedStrings, cellStyles);
                        eventProps.Add(cellValueString);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Failed to parse data at {cell.CellReference}:- {ex.Message}");
                    }
                }
                var wfEvent = ParseWorkingFamiliesEvent(eventProps, columnHeaders);
                var validationResults = validator.Validate(wfEvent);
                if (!validationResults.IsValid) throw new ValidationException($"On row {row.RowIndex}: {validationResults.ToString().ReplaceLineEndings(", ")}");
                DataLoad.Add(wfEvent);
            }
            if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file no content.");
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportWfHMRCData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new WorkingFamiliesEvent())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.ImportWfHMRCData(DataLoad);
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
    }

    private List<string> GetColumnHeaders(Row headerRow, SharedStringTable sharedStrings)
    {
        List<string> columnHeaders = [];
        foreach (Cell cell in headerRow.Elements().Skip(1))
        {
            if (cell.CellValue is null) continue;
            var headerName = cell.CellValue.InnerText;
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                headerName = sharedStrings.ChildElements[int.Parse(headerName)].InnerText;
            }
            columnHeaders.Add(headerName.Trim());
        }
        return columnHeaders;
    }

    private static WorkingFamiliesEvent ParseWorkingFamiliesEvent(List<string> eventProps, List<string> columnHeaders)
    {
        var validityStartDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity Start Date")]));
        var validityEndDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity End Date")]));
        var submissionDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Submission Date")]));
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventId = Guid.NewGuid().ToString(),
            EligibilityCode = eventProps[columnHeaders.IndexOf("Eligibility Code")],
            ValidityStartDate = validityStartDate,
            ValidityEndDate = validityEndDate,
            ParentNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Parent NINO")],
            ParentFirstName = eventProps[columnHeaders.IndexOf("Parent Forename")],
            ParentLastName = eventProps[columnHeaders.IndexOf("Parent Surname")],
            ChildFirstName = eventProps[columnHeaders.IndexOf("Child Forename")],
            ChildLastName = eventProps[columnHeaders.IndexOf("Child Surname")],
            ChildDateOfBirth = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Child DOB")])),
            PartnerNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Partner NINO")],
            PartnerFirstName = eventProps[columnHeaders.IndexOf("Partner Forename")],
            PartnerLastName = eventProps[columnHeaders.IndexOf("Partner Surname")],
            SubmissionDate = submissionDate,
            DiscretionaryValidityStartDate = GetDiscretionaryStartDate(validityStartDate, submissionDate),
            GracePeriodEndDate = GetGracePeriodEndDate(validityEndDate)
        };

        return wfEvent;
    }

    private static DateTime GetGracePeriodEndDate(DateTime validityEndDate)
    {
        if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 10, 22)) >= 0)
        {
            return new DateTime(validityEndDate.Year + 1, 3, 31);
        }
        else if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 5, 27)) >= 0)
        {
            return new DateTime(validityEndDate.Year, 12, 31);
        }
        else if (validityEndDate.CompareTo(new DateTime(validityEndDate.Year, 2, 11)) >= 0)
        {
            return new DateTime(validityEndDate.Year, 8, 31);
        }
        else
        {
            return new DateTime(validityEndDate.Year, 3, 31);
        }
    }

    private static DateTime GetDiscretionaryStartDate(DateTime validityStartDate, DateTime submissionDate)
    {
        var firstTermStart = new DateTime(validityStartDate.Year, 9, 1);
        var secondTermStart = new DateTime(validityStartDate.Year, 1, 1);
        var thirdTermStart = new DateTime(validityStartDate.Year, 4, 1);
        var termDates = new List<DateTime>([firstTermStart, secondTermStart, thirdTermStart]);

        foreach (DateTime termStart in termDates)
        {
            if (validityStartDate.CompareTo(termStart) > 0 &&
                validityStartDate.CompareTo(termStart.AddDays(13)) <= 0 &&
                submissionDate.CompareTo(termStart) < 0)
            {
                return termStart.AddDays(-1);
            }
        }
        // Else use VSD
        return validityStartDate;
    }

    private string getCellValueString(Cell cell, SharedStringTable sharedStrings, CellFormat[] cellStyles)
    {
        if (cell.CellValue is null)
            return string.Empty;
        string value = cell.CellValue.InnerText.Trim();

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            // Get shared string value
            value = sharedStrings.ChildElements[int.Parse(value)].InnerText.Trim();
            // If the cell is a date field then convert to standard date format OADate
            CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
            if (style.NumberFormatId == 14)
            {
                value = DateTime.ParseExact(value, "dd/MM/yyyy", null).ToOADate().ToString();
            }
        }
        return value;
    }
}