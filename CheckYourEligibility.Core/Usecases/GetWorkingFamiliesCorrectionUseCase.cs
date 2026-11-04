using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Validation;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

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
