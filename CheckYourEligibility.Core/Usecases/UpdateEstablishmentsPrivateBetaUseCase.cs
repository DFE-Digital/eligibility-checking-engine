using System.Globalization;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.CsvImport;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.UseCases;

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
    }
}
