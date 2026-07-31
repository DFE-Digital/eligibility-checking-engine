using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using DocumentFormat.OpenXml.Presentation;
using FluentValidation;
using System;
using Error = CheckYourEligibility.API.Boundary.Responses.Error;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Usecases.Internal;

/// <summary>
///     Interface for processing a single eligibility check
/// </summary>
public interface ICheckWorkingFamiliesUseCase
{
  /// <summary>
  /// Apply buisiness logic for interal based eligibility checks
  /// </summary>
  /// <param name="eligibilityResponse"></param>
  /// <returns></returns>
    Task<CheckWorkingFamiliesResponse> Execute(string guid, DateTime checkDate);
}

public class CheckWorkingFamiliesUseCase : ICheckWorkingFamiliesUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckEligibility _checkGateway;
    private readonly ILogger<CheckWorkingFamiliesUseCase> _logger;
    private readonly IValidator<IEligibilityServiceType> _validator;
    private readonly IGetEligibilityCheckItemUseCase _getEligibilityCheckItemUseCase;

    public CheckWorkingFamiliesUseCase(
        ICheckEligibility checkGateway,
        IAudit auditGateway,
        IValidator<IEligibilityServiceType> validator,
       IGetEligibilityCheckItemUseCase getEligibilityCheckItemUseCase,
    ILogger<CheckWorkingFamiliesUseCase> logger)
    {
        _checkGateway = checkGateway;
        _auditGateway = auditGateway;
        _validator = validator;
        _getEligibilityCheckItemUseCase = getEligibilityCheckItemUseCase;
        _logger = logger;
    }

    public async Task<CheckWorkingFamiliesResponse> Execute(string guid, DateTime checkDate)
    {
        // get item
        var result = await _getEligibilityCheckItemUseCase.Execute(guid, CheckEligibilityType.None);
        // read result
      
        // apply business logic from helpers for
        // DVSD applied
        // term validity
        // reconfirmation properties

        // change hyper links to internal.

        return new CheckWorkingFamiliesResponse();
    }
}