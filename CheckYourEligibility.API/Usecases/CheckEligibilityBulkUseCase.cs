using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.UseCases;

public interface ICheckEligibilityBulkUseCase
{
    Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk model,
        int recordCountLimit);
}

public class CheckEligibilityBulkUseCase : ICheckEligibilityBulkUseCase

{
    private readonly IValidator<CheckEligibilityRequestData> _validator;
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityBulkUseCase> _logger;

    public CheckEligibilityBulkUseCase(
        IValidator<CheckEligibilityRequestData> validator,
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        ILogger<CheckEligibilityBulkUseCase> logger)
    {
        _validator = validator;
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityResponseBulk> Execute(
        CheckEligibilityRequestBulk model,
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

        var errors = new StringBuilder();
        int index = 1;

        foreach (var item in model.Data)
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
            throw new ValidationException(null, errors.ToString());

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
}