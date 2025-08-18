using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
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
        try
        {
            //TODO: Read data from the excel file
            //Calculate the discretionary and grace period dates for each entry
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
}