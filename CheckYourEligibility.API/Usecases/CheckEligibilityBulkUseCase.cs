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
    Task<CheckEligibilityResponseBulk> Execute<T>(
        T model,
        CheckEligibilityType type,
        int recordCountLimit) where T : CheckEligibilityRequestBulkBase;
}

public class CheckEligibilityBulkUseCase : ICheckEligibilityBulkUseCase

{
    private readonly IValidator<IEligibilityServiceType> _validator;
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckEligibilityBulkUseCase> _logger;

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

    public async Task<CheckEligibilityResponseBulk> Execute<T>(
        T model,
        CheckEligibilityType type,
        int recordCountLimit) where T : CheckEligibilityRequestBulkBase
    {
        var modelBulk = EligibilityBulkModelFactory.CreateBulkFromGeneric(model, type);
        var bulkData = (modelBulk as dynamic).Data;
        if (modelBulk == null || bulkData == null)

            throw new ValidationException(null, "Invalid Request, data is required.");

        if (bulkData.Count > recordCountLimit)
        {
            var errorMessage =
                $"Invalid Request, data limit of {recordCountLimit} exceeded, {bulkData.Count} records.";
            _logger.LogWarning(errorMessage);
            throw new ValidationException(null, errorMessage);
        }

        List<Error> errors = new List<Error>();

        int index = 1;

        foreach (var item in bulkData)
        {
            item.NationalInsuranceNumber = item.NationalInsuranceNumber?.ToUpperInvariant();
            if (type != CheckEligibilityType.WorkingFamilies)
            {
                item.NationalAsylumSeekerServiceNumber = item.NationalAsylumSeekerServiceNumber?.ToUpperInvariant();
            }

            var result = _validator.Validate(item);
            if (!result.IsValid)
            {
                for (int i = 0; i < result.Errors.Count; i++)
                {
                    Error error = new Error
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = result.Errors[i].ToString(),
                        Detail = item.ClientIdentifier
                    };
                    errors.Add(error);
                }
            }

            index++;
        }

        if (errors.Count > 0)
            throw new ValidationException(errors, string.Empty);

        var groupId = Guid.NewGuid().ToString();
        await _checkGateway.PostCheck(bulkData, groupId);

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