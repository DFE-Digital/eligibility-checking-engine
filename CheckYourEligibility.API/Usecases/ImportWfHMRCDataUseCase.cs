using System.Collections.Immutable;
using System.Xml.Linq;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;

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
        if (file == null || file.ContentType.ToLower() != "text/xml")
            throw new InvalidDataException($"{Admin.XmlfileRequired}");
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
                    CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
                    var cellValueString = getCellValueString(cell, sharedStrings, cellStyles);
                    eventProps.Add(cellValueString);
                }
                var wfEvent = ParseWorkingFamiliesEvent(eventProps, columnHeaders);
                //TODO: Put in a try/catch. If an exception is thrown then maintain a list of errors that can be returned
                // Or just let the outer catch handle it?
                DataLoad.Add(wfEvent);
            }
            if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file no content.");
            //Check if the event already exists? to avoid duplicates?
            //Consider renaming functions and endpoint to help distinguish from the FSM HMRC import
            //Does this need a new db migration for the bulk inserts?
            //Can it accept non macro-enabled excel files???
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportWfHMRCData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new WorkingFamiliesEvent())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        //await _gateway.ImportWfHMRCData(DataLoad);
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

/*
    private static IEnumerable<Row> GetUsedRows(IEnumerable<Row> dataRows)
    {
        var result = from obj in dataRows
                     where obj.ChildElements.ElementAt(1) is not null
                     select obj;
        //Iterate all rows except the first one.
        foreach (Row row in dataRows)
        {
            // Check the 'number' index
            if (row.Elements<Cell>().ElementAt(1).CellValue is not null)
            {
                yield return row;
            }
            else
            {
                return;
            }
        }
    }
    */

    private static WorkingFamiliesEvent ParseWorkingFamiliesEvent(List<string> eventProps, List<string> columnHeaders)
    {
        WorkingFamiliesEvent wfEvent = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventId = Guid.NewGuid().ToString(),
            EligibilityCode = eventProps[columnHeaders.IndexOf("Eligibility Code")],
            ValidityStartDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity Start Date")])),
            ValidityEndDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Validity End Date")])),
            ParentNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Parent NINO")],
            ParentFirstName = eventProps[columnHeaders.IndexOf("Parent Forename")],
            ParentLastName = eventProps[columnHeaders.IndexOf("Parent Surname")],
            ChildFirstName = eventProps[columnHeaders.IndexOf("Child Forename")],
            ChildLastName = eventProps[columnHeaders.IndexOf("Child Surname")],
            ChildDateOfBirth = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Child DOB")])),
            PartnerNationalInsuranceNumber = eventProps[columnHeaders.IndexOf("Partner NINO")], //TODO: Do partner details need to be nullable?
            PartnerFirstName = eventProps[columnHeaders.IndexOf("Partner Forename")],
            PartnerLastName = eventProps[columnHeaders.IndexOf("Partner Surname")],
            SubmissionDate = DateTime.FromOADate(int.Parse(eventProps[columnHeaders.IndexOf("Submission Date")])),
            DiscretionaryValidityStartDate = DateTime.UtcNow,
            GracePeriodEndDate = DateTime.UtcNow
        };

        //Map the row to the event
        //Determine the DVSD and GPED
        //Throw exception if can't parse the data
        return wfEvent;
    }

    private string getCellValueString(Cell cell, SharedStringTable sharedStrings, CellFormat[] cellStyles) {
        if (cell.CellValue is null)
            return string.Empty;
        string value = cell.CellValue.InnerText;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            // Get shared string value
            value = sharedStrings.ChildElements[int.Parse(value)].InnerText;
            // If the cell is a date field then convert to standard date format OADate
            CellFormat style = cellStyles[int.Parse(cell.StyleIndex.InnerText)];
            if (style.NumberFormatId == 14)
            {
                value = DateTime.Parse(value).ToOADate().ToString();
            }
        }
        return value;
    }
}