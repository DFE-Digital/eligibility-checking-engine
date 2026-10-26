using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;

namespace CheckYourEligibility.API.UseCases;

public interface IGetWorkingFamiliesCorrectionUseCase
{
    Task<WorkingFamiliesCorrectionResponse> Execute(
        string? eligibilityCode,
        CancellationToken cancellationToken = default);
}

public class GetWorkingFamiliesCorrectionUseCase
    : IGetWorkingFamiliesCorrectionUseCase
{
    private readonly IWorkingFamiliesCorrection _gateway;

    public GetWorkingFamiliesCorrectionUseCase(
        IWorkingFamiliesCorrection gateway)
    {
        _gateway = gateway;
    }

    public async Task<WorkingFamiliesCorrectionResponse> Execute(
        string? eligibilityCode,
        CancellationToken cancellationToken = default)
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

        var record = await _gateway.GetByEligibilityCode(
            code, cancellationToken);

        if (record is null)
        {
            throw new NotFoundException(
                "No Working Families correction record was found.");
        }

        return new WorkingFamiliesCorrectionResponse
        {
            Data = record
        };
    }
}
