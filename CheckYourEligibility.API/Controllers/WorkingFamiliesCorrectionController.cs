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
    private readonly ILogger<WorkingFamiliesCorrectionController> _logger;

    public WorkingFamiliesCorrectionController(
        IGetWorkingFamiliesCorrectionUseCase useCase,
        ILogger<WorkingFamiliesCorrectionController> logger)
    {
        _useCase = useCase;
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