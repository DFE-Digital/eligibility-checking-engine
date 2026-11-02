using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Helpers;

namespace CheckYourEligibility.API.Domain.Validation;

internal static class WorkingFamiliesCorrectionCodeValidation
{
    internal static string ValidateAndNormalize(string? eligibilityCode)
    {
        var code = eligibilityCode?.Trim();

        if (string.IsNullOrEmpty(code)
            || code.Length != 11
            || code.Any(character => character < '0' || character > '9'))
        {
            const string message = "Enter an 11-digit eligibility code.";

            throw new ValidationException(
                new List<Error>
                {
                    new()
                    {
                        Title = "validation_error",
                        Detail = message
                    }
                },
                message);
        }

        if (WorkingFamiliesCheckHelper.GetEligibilityCodeType(code)
            == EligibilityCodeType.Foster)
        {
            throw new FosterCodeCorrectionNotSupportedException();
        }

        return code;
    }
}