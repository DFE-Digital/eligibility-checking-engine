using System.Globalization;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.CsvImport;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.UseCases;

public interface IImportFsmHomeOfficeDataUseCase
{
    Task Execute(IFormFile file);
}

public class ImportFsmHomeOfficeDataUseCase : IImportFsmHomeOfficeDataUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly ILogger<ImportFsmHomeOfficeDataUseCase> _logger;

    public ImportFsmHomeOfficeDataUseCase(IAdministration Gateway, IAudit auditGateway,
        ILogger<ImportFsmHomeOfficeDataUseCase> logger)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<FreeSchoolMealsHO> DataLoad;
        if (file == null || file.ContentType.ToLower() != "text/csv")
            throw new InvalidDataException($"{Admin.CsvfileRequired}");
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                BadDataFound = null,
                MissingFieldFound = null
            };
            using (var fileStream = file.OpenReadStream())
            using (var csv = new CsvReader(new StreamReader(fileStream), config))
            {
                csv.Context.RegisterClassMap<HomeOfficeRowMap>();
                var records = csv.GetRecords<HomeOfficeRow>();

                DataLoad = records.Select(x => new FreeSchoolMealsHO
                {
                    FreeSchoolMealsHOID = Guid.NewGuid().ToString(),
                    NASS = x.Nas,
                    LastName = x.Surname,
                    DateOfBirth = DateTime.ParseExact(x.Dob, "yyyyMMdd", CultureInfo.InvariantCulture)
                }).ToList();
                if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file content.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportHomeOfficeData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new HomeOfficeRow())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.ImportHomeOfficeData(DataLoad);
    }
}