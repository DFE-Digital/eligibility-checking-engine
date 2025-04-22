using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using FeatureManagement.Domain.Validation;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for processing bulk FSM eligibility checks
/// </summary>
public interface ICheckEligibilityBulkUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="model">Bulk FSM eligibility check request</param>
    /// <param name="recordCountLimit">Maximum allowed records in a bulk upload</param>
    /// <returns>Check eligibility bulk response or validation errors</returns>
    Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk_Fsm model,
        int recordCountLimit);
}

public class CheckEligibilityBulkUseCase : ICheckEligibilityBulkUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityBulkUseCase> _logger;

    public CheckEligibilityBulkUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<CheckEligibilityBulkUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk_Fsm model,
        int recordCountLimit)
    {
        if (model == null || model.Data == null)
            throw new ValidationException(null, "Invalid Request, data is required.");

        if (model.Data.Count() > recordCountLimit)
        {
            var errorMessage =
                $"Invalid Request, data limit of {recordCountLimit} exceeded, {model.Data.Count()} records.";
            _logger.LogWarning(errorMessage);
            throw new ValidationException(null, errorMessage);
        }

        if (model.GetType() != typeof(CheckEligibilityRequestBulk_Fsm))
            throw new ValidationException(null, $"Unknown request type:-{model.GetType()}");

        var validationErrors = ValidateBulkItems(model);
        if (validationErrors.Length > 0)
        {
            _logger.LogWarning("Validation errors in bulk eligibility check");
            throw new ValidationException(null, validationErrors.ToString());
        }

        var groupId = Guid.NewGuid().ToString();
        await _checkGateway.PostCheck(model.Data, groupId);

        await _auditGateway.CreateAuditEntry(AuditType.BulkCheck, groupId);


        _logger.LogInformation($"Bulk eligibility check created with group ID: {groupId}");

        return new CheckEligibilityResponseBulk
        {
            Data = new StatusValue { Status = $"{Messages.Processing}" },
            Links = new CheckEligibilityResponseBulkLinks
            {
                Get_Progress_Check = $"{CheckLinks.BulkCheckLink}{groupId}{CheckLinks.BulkCheckProgress}",
                Get_BulkCheck_Results = $"{CheckLinks.BulkCheckLink}{groupId}{CheckLinks.BulkCheckResults}"
            }
        };
    }

    private static StringBuilder ValidateBulkItems(CheckEligibilityRequestBulk_Fsm model)
    {
        var validationResultsItems = new StringBuilder();
        var validator = new CheckEligibilityRequestDataValidator_Fsm();
        var sequence = 1;

        foreach (var item in model.Data)
        {
            item.NationalInsuranceNumber = item.NationalInsuranceNumber?.ToUpper();
            item.NationalAsylumSeekerServiceNumber = item.NationalAsylumSeekerServiceNumber?.ToUpper();

            var validationResults = validator.Validate(item);
            if (!validationResults.IsValid)
                validationResultsItems.AppendLine($"Item:-{sequence}, {validationResults.ToString()}");

            sequence++;
        }

        return validationResultsItems;
    }
}