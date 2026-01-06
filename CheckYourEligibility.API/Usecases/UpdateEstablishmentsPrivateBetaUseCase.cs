using System.Globalization;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateEstablishmentsPrivateBetaUseCase
{
    Task Execute(IFormFile file);
}

public class UpdateEstablishmentsPrivateBetaUseCase : IUpdateEstablishmentsPrivateBetaUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly ILogger<UpdateEstablishmentsPrivateBetaUseCase> _logger;

    public UpdateEstablishmentsPrivateBetaUseCase(IAdministration gateway, IAudit auditGateway,
        ILogger<UpdateEstablishmentsPrivateBetaUseCase> logger)
    {
        _gateway = gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<EstablishmentPrivateBetaRow> dataLoad;
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
                csv.Context.RegisterClassMap<EstablishmentPrivateBetaRowMap>();
                dataLoad = csv.GetRecords<EstablishmentPrivateBetaRow>().ToList();

                if (dataLoad == null || dataLoad.Count == 0) throw new InvalidDataException("Invalid file content.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("UpdateEstablishmentsPrivateBeta", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new EstablishmentPrivateBetaRow())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.UpdateEstablishmentsPrivateBeta(dataLoad);
        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
    }
}
