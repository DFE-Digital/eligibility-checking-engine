using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for bulk eligibility checking operations
/// </summary>
public interface ICheckEligibilityBulkUseCase
{
    /// <summary>
    /// Executes a bulk eligibility check operation
    /// </summary>
    /// <param name="model">The bulk eligibility check request</param>
    /// <param name="type">The type of eligibility check to perform</param>
    /// <param name="recordCountLimit">Maximum number of records allowed in a bulk operation</param>
    /// <returns>A response containing the bulk check status and links</returns>
    Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk model,
        CheckEligibilityType type,
        int recordCountLimit);
}

/// <summary>
/// Use case implementation for bulk eligibility checking operations
/// </summary>
public class CheckEligibilityBulkUseCase : ICheckEligibilityBulkUseCase

{
    private readonly IValidator<IEligibilityServiceType> _validator;
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityBulkUseCase> _logger;

    /// <summary>
    /// Use case for bulk eligibility checking operations
    /// </summary>
    public CheckEligibilityBulkUseCase(
        IValidator<IEligibilityServiceType> validator,
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<CheckEligibilityBulkUseCase> logger)
    {
        _validator = validator;
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    /// <summary>
    /// Executes a bulk eligibility check operation
    /// </summary>
    /// <param name="model">The bulk eligibility check request</param>
    /// <param name="type">The type of eligibility check to perform</param>
    /// <param name="recordCountLimit">Maximum number of records allowed in a bulk operation</param>
    /// <returns>A response containing the bulk check status and links</returns>
    public async Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk model,
        CheckEligibilityType type,
        int recordCountLimit)
    {
        var modelData = EligibilityBulkModelFactory.CreateBulkFromGeneric(model, type);

        if (modelData == null || model.Data == null)
            throw new ValidationException([], "Invalid Request, data is required.");

        if (modelData.Data.Count() > recordCountLimit)
        {
            var errorMessage =
                $"Invalid Request, data limit of {recordCountLimit} exceeded, {model.Data.Count()} records.";
            _logger.LogWarning(errorMessage);
            throw new ValidationException([], errorMessage);
        }

        var errors = new StringBuilder();
        int index = 1;

        foreach (var item in modelData.Data)
        {
            item.NationalInsuranceNumber = item.NationalInsuranceNumber?.ToUpperInvariant();
            item.NationalAsylumSeekerServiceNumber = item.NationalAsylumSeekerServiceNumber?.ToUpperInvariant();

            var result = _validator.Validate(item);
            if (!result.IsValid)
            {
                errors.AppendLine($"Item {index}: {result}");
            }

            index++;
        }

        if (errors.Length > 0)
            throw new ValidationException([], errors.ToString());

        var groupId = Guid.NewGuid().ToString();
        
        // Create BulkCheck record via gateway
        var bulkCheck = new Domain.BulkCheck
        {
            Guid = groupId,
            ClientIdentifier = model.ClientIdentifier ?? string.Empty,
            Filename = model.Filename ?? string.Empty,
            SubmittedBy = model.SubmittedBy ?? string.Empty,
            EligibilityType = type,
            Status = BulkCheckStatus.InProgress,
            SubmittedDate = DateTime.UtcNow
        };

        await _checkGateway.CreateBulkCheck(bulkCheck);

        await _checkGateway.PostCheck(modelData.Data, groupId);

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
}