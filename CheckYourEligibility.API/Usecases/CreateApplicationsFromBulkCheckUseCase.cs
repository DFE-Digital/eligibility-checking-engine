using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateApplicationsFromBulkCheckUseCase
{
    Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds);
    Task ProcessApplicationsFromBulkCheck(
        string bulkCheckId,
        List<EligibilityCheck> eligibleChecks,
        List<int> allowedLocalAuthorityIds);
}

public class CreateApplicationsFromBulkCheckUseCase : ICreateApplicationsFromBulkCheckUseCase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreateApplicationsFromBulkCheckUseCase> _logger;
    private readonly ICreateApplicationUseCase _createApplicationUseCase;
    private readonly ICreateApplicationsFromBulkCheck _createApplicationsFromBulkCheckGateway;

    public CreateApplicationsFromBulkCheckUseCase(
    IServiceScopeFactory scopeFactory,
    ILogger<CreateApplicationsFromBulkCheckUseCase> logger,
    ICreateApplicationUseCase createApplicationUseCase,
    ICreateApplicationsFromBulkCheck createApplicationsFromBulkCheckGateway)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _createApplicationUseCase = createApplicationUseCase;
        _createApplicationsFromBulkCheckGateway = createApplicationsFromBulkCheckGateway;
    }

    public async Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds)
    {
        var bulkCheck = await _createApplicationsFromBulkCheckGateway
            .GetBulkCheck(bulkCheckId);

        if (bulkCheck == null)
        {
            throw new NotFoundException($"Bulk check {bulkCheckId} not found");
        }

        if (bulkCheck.Status != BulkCheckStatus.Completed)
        {
            throw new ValidationException(
            [
                new Error
            {
                Title = "Applications can only be created when bulk check status is 'Completed'"
            }
            ],
            "Invalid bulk check status");
        }

        var eligibleChecks = await _createApplicationsFromBulkCheckGateway
            .GetEligibleChecks(bulkCheckId);

        if (!eligibleChecks.Any())
        {
            throw new ValidationException(
            [
                new Error
            {
                Title = "No eligible checks found for this bulk check"
            }
            ],
            "No eligible checks found");
        }

        await _createApplicationsFromBulkCheckGateway.UpdateBulkCheckStatus(
            bulkCheckId,
            BulkCheckStatus.ApplicationCreationInProgress);

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();

            var scopedUseCase =
                scope.ServiceProvider.GetRequiredService<ICreateApplicationsFromBulkCheckUseCase>();

            try
            {
                await scopedUseCase.ProcessApplicationsFromBulkCheck(
                    bulkCheckId,
                    eligibleChecks,
                    allowedLocalAuthorityIds);
            }
            catch (Exception ex)
            {
                var sanitizedBulkCheckId = SanitizeForLog(bulkCheckId);

                _logger.LogError(
                    ex,
                    "Application creation failed for bulk check {BulkCheckId}",
                    sanitizedBulkCheckId);
            }
        });

        return new MessageResponse
        {
            Data = "Application creation started."
        };
    }

    /// <summary>
    /// Removes line breaks before logging user-provided values to prevent log injection attacks. 
    /// This is a basic sanitization step and may need to be enhanced based on specific security requirements.
    /// </summary>
    /// <param name="input"></param>
    /// <returns>Cleaned string</returns>
    private static string SanitizeForLog(string input)
    {
        return (input ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

    /// <summary>
    /// Determines whether a name value is missing or fails validation rules,
    /// and logs a warning when the value is invalid.
    /// </summary>
    /// <param name="value">The name value to validate.</param>
    /// <param name="fieldName">The field name being validated.</param>
    /// <param name="eligibilityCheckId">The related eligibility check identifier.</param>
    /// <returns>
    /// True when the value is missing or invalid; otherwise false.
    /// </returns>
    private bool InvalidName(
               string? value,
               string fieldName,
               string eligibilityCheckId)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DataValidation.BeAValidName(value))
        {
            return false;
        }

        _logger.LogWarning(
            "Skipping eligibility check {EligibilityCheckId} because {FieldName} is missing or invalid",
            eligibilityCheckId,
            fieldName);

        return true;
    }

    public async Task ProcessApplicationsFromBulkCheck(
        string bulkCheckId,
        List<EligibilityCheck> eligibleChecks,
        List<int> allowedLocalAuthorityIds)
    {
        var hasFailures = false;

        foreach (var check in eligibleChecks)
        {
            var checkData = JsonConvert.DeserializeObject<CheckProcessData>(check.CheckData);

            if (checkData == null)
            {
                hasFailures = true;

                _logger.LogWarning(
                    "Skipping eligibility check {EligibilityCheckId} because CheckData could not be deserialised",
                    check.EligibilityCheckID);

                continue;
            }

            if (InvalidName(checkData.FirstName, "FirstName", check.EligibilityCheckID))
            {
                hasFailures = true;
                continue;
            }

            if (InvalidName(checkData.ChildFirstName, "ChildFirstName", check.EligibilityCheckID))
            {
                hasFailures = true;
                continue;
            }

            if (InvalidName(checkData.ChildLastName, "ChildLastName", check.EligibilityCheckID))
            {
                hasFailures = true;
                continue;
            }

            if (!int.TryParse(checkData.ChildSchoolURN, out var establishment) || establishment <= 0)
            {
                hasFailures = true;

                _logger.LogWarning(
                    "Skipping eligibility check {EligibilityCheckId} because ChildSchoolURN is missing or invalid",
                    check.EligibilityCheckID);

                continue;
            }          

            var applicationRequest = new ApplicationRequest
            {
                Data = new ApplicationRequestData
                {
                    Type = CheckEligibilityType.FreeSchoolMeals,
                    Establishment = establishment,
                    ParentFirstName = checkData.FirstName!,
                    ParentLastName = checkData.LastName!,
                    ParentEmail = checkData.EmailAddress,
                    ParentNationalInsuranceNumber = checkData.NationalInsuranceNumber,
                    ParentNationalAsylumSeekerServiceNumber = checkData.NationalAsylumSeekerServiceNumber,
                    ParentDateOfBirth = checkData.DateOfBirth,
                    ChildFirstName = checkData.ChildFirstName!,
                    ChildLastName = checkData.ChildLastName!,
                    ChildDateOfBirth = checkData.ChildDateOfBirth!,
                    UserId = null,
                    Evidence = []
                }
            };

            try
            {
                await _createApplicationUseCase.Execute(applicationRequest, allowedLocalAuthorityIds);
            }
            catch (Exception ex)
            {
                hasFailures = true;

                _logger.LogError(
                    ex,
                    "Application creation failed for eligibility check {EligibilityCheckId}",
                    check.EligibilityCheckID);
            }
        }

        await _createApplicationsFromBulkCheckGateway.UpdateBulkCheckStatus(
            bulkCheckId,
            hasFailures
                ? BulkCheckStatus.ApplicationCreationFailed
                : BulkCheckStatus.ApplicationsCreated);
    }
}