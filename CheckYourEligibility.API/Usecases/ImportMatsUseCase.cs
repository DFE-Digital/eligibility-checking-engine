using System.Globalization;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

public interface IImportMatsUseCase
{
    Task Execute(IFormFile file);
}

public class ImportMatsUseCase : IImportMatsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly ILogger<ImportMatsUseCase> _logger;

    public ImportMatsUseCase(IAdministration Gateway, IAudit auditGateway,
        ILogger<ImportMatsUseCase> logger)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<MatRow> DataLoad;
        if (file == null || file.ContentType.ToLower() != "text/csv")
            throw new InvalidDataException($"{Admin.CsvfileRequired}");
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                BadDataFound = null,
                MissingFieldFound = null
            };
            using (var fileStream = file.OpenReadStream())
            using (var csv = new CsvReader(new StreamReader(fileStream), config))
            {
                csv.Context.RegisterClassMap<MatRowMap>();
                DataLoad = csv.GetRecords<MatRow>().ToList();

                if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file content.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportMatData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new MatRow())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.ImportMats(DataLoad);
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
    }
}