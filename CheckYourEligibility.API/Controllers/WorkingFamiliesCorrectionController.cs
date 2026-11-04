using System.Text.Json;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("working-families/{eligibilityCode}/correction")]
[Authorize(Policy = PolicyNames.ManageWorkingFamiliesCorrections)]
public class WorkingFamiliesCorrectionController : ControllerBase
{
    private readonly IGetWorkingFamiliesCorrectionUseCase _useCase;
    private readonly IUpdateWorkingFamiliesCorrectionUseCase _updateUseCase;
    private readonly ILogger<WorkingFamiliesCorrectionController> _logger;

    public WorkingFamiliesCorrectionController(
        IGetWorkingFamiliesCorrectionUseCase useCase,
        IUpdateWorkingFamiliesCorrectionUseCase updateUseCase,
        ILogger<WorkingFamiliesCorrectionController> logger)
    {
        _useCase = useCase;
        _updateUseCase = updateUseCase;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(WorkingFamiliesCorrectionResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ErrorResponse),
        StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(
        typeof(ErrorResponse),
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkingFamiliesCorrectionResponse>> Get(
        string eligibilityCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _useCase.Execute(
                eligibilityCode, cancellationToken);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Errors = ex.Errors.Select(error => new Error
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = error.Title,
                    Detail = error.Detail
                }).ToList()
            });
        }
        catch (FosterCodeCorrectionNotSupportedException)
        {
            return ErrorResult(
                StatusCodes.Status422UnprocessableEntity,
                "foster_code_not_supported",
                "Foster-family codes cannot be corrected through this service.");
        }
        catch (NotFoundException)
        {
            return ErrorResult(
                StatusCodes.Status404NotFound,
                "record_not_found",
                "No matching Working Families record was found.");
        }
        catch (WorkingFamiliesCorrectionDataException ex)
        {
            return TechnicalError(
                ex,
                StatusCodes.Status409Conflict,
                "inconsistent_record");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TechnicalError(
                ex,
                StatusCodes.Status500InternalServerError,
                "technical_error");
        }
    }

    [HttpPut("child")]
    [ProducesResponseType(
    typeof(WorkingFamiliesCorrectionValidationErrorResponse),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        typeof(WorkingFamiliesCorrectionResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkingFamiliesCorrectionResponse>> UpdateChild(
        string eligibilityCode,
        [FromBody] WorkingFamiliesChildCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _updateUseCase.UpdateChild(
                eligibilityCode, request, cancellationToken);

            return Ok(response);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateError(ex);
        }
    }

    [HttpPut("carer")]
    [ProducesResponseType(
    typeof(WorkingFamiliesCorrectionValidationErrorResponse),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        typeof(WorkingFamiliesCorrectionResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkingFamiliesCorrectionResponse>> UpdateCarer(
        string eligibilityCode,
        [FromBody] WorkingFamiliesAdultCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _updateUseCase.UpdateCarer(
                eligibilityCode, request, cancellationToken);

            return Ok(response);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateError(ex);
        }
    }

    [HttpPut("partner")]
    [ProducesResponseType(
    typeof(WorkingFamiliesCorrectionValidationErrorResponse),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(
    typeof(ErrorResponse),
    StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(
        typeof(WorkingFamiliesCorrectionResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkingFamiliesCorrectionResponse>> UpdatePartner(
        string eligibilityCode,
        [FromBody] WorkingFamiliesAdultCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _updateUseCase.UpdatePartner(
                eligibilityCode, request, cancellationToken);

            return Ok(response);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateError(ex);
        }
    }

    private ActionResult<WorkingFamiliesCorrectionResponse> UpdateError(
        Exception exception)
    {
        if (exception is FluentValidation.ValidationException validationException)
        {
            return BadRequest(new WorkingFamiliesCorrectionValidationErrorResponse
            {
                Errors = validationException.Errors.Select(error =>
                    new WorkingFamiliesCorrectionValidationError
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "validation_error",
                        Detail = error.ErrorMessage,
                        Field = string.IsNullOrWhiteSpace(error.PropertyName)
                            ? "request"
                            : JsonNamingPolicy.CamelCase.ConvertName(
                                error.PropertyName)
                    }).ToList()
            });
        }

        if (exception is ValidationException validationFailure)
        {
            return BadRequest(new WorkingFamiliesCorrectionValidationErrorResponse
            {
                Errors = validationFailure.Errors.Select(error =>
                    new WorkingFamiliesCorrectionValidationError
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = error.Title,
                        Detail = error.Detail,
                        Field = "eligibilityCode"
                    }).ToList()
            });
        }

        if (exception is FosterCodeCorrectionNotSupportedException)
        {
            return ErrorResult(
                StatusCodes.Status422UnprocessableEntity,
                "foster_code_not_supported",
                "Foster-family codes cannot be corrected through this service.");
        }

        if (exception is WorkingFamiliesCorrectionPartnerMissingException
            partnerMissing)
        {
            return ErrorResult(
                StatusCodes.Status422UnprocessableEntity,
                "partner_not_found",
                partnerMissing.Message);
        }

        if (exception is NotFoundException)
        {
            return ErrorResult(
                StatusCodes.Status404NotFound,
                "record_not_found",
                "No matching Working Families record was found.");
        }

        if (exception is WorkingFamiliesCorrectionConflictException)
        {
            return ErrorResult(
                StatusCodes.Status409Conflict,
                "record_update_conflict",
                "An error occurred - a record update conflict was detected - please try again");
        }

        if (exception is WorkingFamiliesCorrectionDataException)
        {
            return UpdateTechnicalError(
                exception,
                StatusCodes.Status409Conflict,
                "inconsistent_record");
        }

        return UpdateTechnicalError(
            exception,
            StatusCodes.Status500InternalServerError,
            "technical_error");
    }

    private ObjectResult UpdateTechnicalError(
        Exception exception,
        int status,
        string code)
    {
        var reference = HttpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Working Families correction update failed. Reference: {Reference}",
            reference);

        return ErrorResult(
            status,
            code,
            $"Unable to update the record. Reference: {reference}");
    }

    private ObjectResult TechnicalError(
        Exception exception,
        int status,
        string code)
    {
        var reference = HttpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Working Families correction lookup failed. Reference: {Reference}",
            reference);

        return ErrorResult(
            status,
            code,
            $"Unable to retrieve the record. Reference: {reference}");
    }

    private ObjectResult ErrorResult(int status, string code, string detail)
    {
        return StatusCode(status, new ErrorResponse
        {
            Errors =
            [
                new Error
                {
                    Status = status,
                    Title = code,
                    Detail = detail
                }
            ]
        });
    }
}