using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FeatureManagement.Domain.Validation;
using FluentValidation;
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

}