using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace CheckYourEligibility.API.UseCases;

public interface IUpdateWorkingFamiliesCorrectionUseCase
{
    Task<WorkingFamiliesCorrectionResponse> UpdateChild(
        string? eligibilityCode,
        WorkingFamiliesChildCorrectionRequest? request,
        CancellationToken cancellationToken = default);

    Task<WorkingFamiliesCorrectionResponse> UpdateCarer(
        string? eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest? request,
        CancellationToken cancellationToken = default);

    Task<WorkingFamiliesCorrectionResponse> UpdatePartner(
        string? eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest? request,
        CancellationToken cancellationToken = default);
}

public class UpdateWorkingFamiliesCorrectionUseCase
    : IUpdateWorkingFamiliesCorrectionUseCase
{
    private readonly IWorkingFamiliesCorrection _gateway;

    private readonly WorkingFamiliesChildCorrectionRequestValidator
        _childValidator = new();

    private readonly WorkingFamiliesAdultCorrectionRequestValidator
        _adultValidator = new();

    public UpdateWorkingFamiliesCorrectionUseCase(
        IWorkingFamiliesCorrection gateway)
    {
        _gateway = gateway;
    }

    public Task<WorkingFamiliesCorrectionResponse> UpdateChild(
        string? eligibilityCode,
        WorkingFamiliesChildCorrectionRequest? request,
        CancellationToken cancellationToken = default)
    {
        return Execute(
            eligibilityCode,
            request,
            _childValidator,
            _gateway.UpdateChild,
            cancellationToken);
    }

    public Task<WorkingFamiliesCorrectionResponse> UpdateCarer(
        string? eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest? request,
        CancellationToken cancellationToken = default)
    {
        return Execute(
            eligibilityCode,
            request,
            _adultValidator,
            _gateway.UpdateCarer,
            cancellationToken);
    }

    public Task<WorkingFamiliesCorrectionResponse> UpdatePartner(
        string? eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest? request,
        CancellationToken cancellationToken = default)
    {
        return Execute(
            eligibilityCode,
            request,
            _adultValidator,
            _gateway.UpdatePartner,
            cancellationToken);
    }

    private static async Task<WorkingFamiliesCorrectionResponse> Execute<TRequest>(
        string? eligibilityCode,
        TRequest? request,
        IValidator<TRequest> validator,
        Func<string, TRequest, CancellationToken,
            Task<WorkingFamiliesCorrectionRecord?>> update,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var code =
            WorkingFamiliesCorrectionCodeValidation.ValidateAndNormalize(
                eligibilityCode);

        if (request is null)
        {
            throw new FluentValidation.ValidationException(
                new[]
                {
                    new ValidationFailure(
                        "request",
                        "A correction request body is required.")
                });
        }

        var validationResult = await validator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(
                validationResult.Errors);
        }

        var record = await update(code, request, cancellationToken);

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