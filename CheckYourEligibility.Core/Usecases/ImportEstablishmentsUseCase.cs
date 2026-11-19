using System.Globalization;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.CsvImport;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.UseCases;

public interface IImportEstablishmentsUseCase
{
    Task Execute(IFormFile file);
}

public class ImportEstablishmentsUseCase : IImportEstablishmentsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly ILogger<ImportEstablishmentsUseCase> _logger;

    public ImportEstablishmentsUseCase(IAdministration Gateway, IAudit auditGateway,
        ILogger<ImportEstablishmentsUseCase> logger)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<EstablishmentRow> DataLoad;
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
                csv.Context.RegisterClassMap<EstablishmentRowMap>();
                DataLoad = csv.GetRecords<EstablishmentRow>().ToList();

                if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file content.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportEstablishmentData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new EstablishmentRow())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.ImportEstablishments(DataLoad);
    }
}