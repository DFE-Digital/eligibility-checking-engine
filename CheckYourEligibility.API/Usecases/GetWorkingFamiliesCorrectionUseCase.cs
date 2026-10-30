using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;

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
        var code =
            WorkingFamiliesCorrectionCodeValidation.ValidateAndNormalize(
                eligibilityCode);

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
