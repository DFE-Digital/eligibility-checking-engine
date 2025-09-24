// Ignore Spelling: Fsm

using System.Net;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

/// <summary>
///     Administration Controller
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class AdministrationController : BaseController
{
    private readonly ICleanUpEligibilityChecksUseCase _cleanUpEligibilityChecksUseCase;
    private readonly ICleanUpRateLimitEventsUseCase _cleanUpRateLimitEventsUseCase;
    private readonly IImportEstablishmentsUseCase _importEstablishmentsUseCase;
    private readonly IImportMatsUseCase _importMatsUseCase;
    private readonly IImportFsmHMRCDataUseCase _importFsmHMRCDataUseCase;
    private readonly IImportFsmHomeOfficeDataUseCase _importFsmHomeOfficeDataUseCase;
    private readonly IImportWfHMRCDataUseCase _importWfHMRCDataUseCase;

    /// <summary>
    ///     Constructor for AdministrationController
    /// </summary>
    /// <param name="cleanUpEligibilityChecksUseCase"></param>
    /// <param name="cleanUpRateLimitEventsUseCase"></param>
    /// <param name="importEstablishmentsUseCase"></param>
    /// <param name="importMatsUseCase"></param>
    /// <param name="importFsmHomeOfficeDataUseCase"></param>
    /// <param name="importFsmHMRCDataUseCase"></param>
    /// <param name="audit"></param>
    public AdministrationController(
        ICleanUpEligibilityChecksUseCase cleanUpEligibilityChecksUseCase,
        ICleanUpRateLimitEventsUseCase cleanUpRateLimitEventsUseCase,
        IImportEstablishmentsUseCase importEstablishmentsUseCase,
        IImportMatsUseCase importMatsUseCase,
        IImportFsmHomeOfficeDataUseCase importFsmHomeOfficeDataUseCase,
        IImportFsmHMRCDataUseCase importFsmHMRCDataUseCase,
        IImportWfHMRCDataUseCase importWfHMRCDataUseCase,
        IAudit audit) : base(audit)
    {
        _cleanUpEligibilityChecksUseCase = cleanUpEligibilityChecksUseCase;
        _cleanUpRateLimitEventsUseCase = cleanUpRateLimitEventsUseCase;
        _importEstablishmentsUseCase = importEstablishmentsUseCase;
        _importMatsUseCase = importMatsUseCase;
        _importFsmHomeOfficeDataUseCase = importFsmHomeOfficeDataUseCase;
        _importFsmHMRCDataUseCase = importFsmHMRCDataUseCase;
        _importWfHMRCDataUseCase = importWfHMRCDataUseCase;
    }

    /// <summary>
    ///     Deletes all old Rate Limit Events based on the service configuration
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPut("/admin/clean-up-rate-limit-events")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> CleanUpRateLimitEvents()
    {
        await _cleanUpRateLimitEventsUseCase.Execute();
        return new ObjectResult(new MessageResponse { Data = $"{Admin.RateLimitEventCleanse}" })
        { StatusCode = StatusCodes.Status200OK };
    }

    /// <summary>
    ///     Imports Establishments
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/admin/import-establishments")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> ImportEstablishments(IFormFile file)
    {
        try
        {
            await _importEstablishmentsUseCase.Execute(file);
            return new ObjectResult(new MessageResponse
            { Data = $"{file.FileName} - {Admin.EstablishmentFileProcessed}" })
            { StatusCode = StatusCodes.Status200OK };
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Imports MultiAcademyTrust data
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/admin/import-multi-academy-trusts")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> ImportMultiAcademyTrusts(IFormFile file)
    {
        try
        {
            await _importMatsUseCase.Execute(file);
            return new ObjectResult(new MessageResponse
            { Data = $"{file.FileName} - {Admin.MatFileProcessed}" })
            { StatusCode = StatusCodes.Status200OK };
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Truncates FsmHomeOfficeData and imports a new data set from CSV input
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/admin/import-home-office-data")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> ImportFsmHomeOfficeData(IFormFile file)
    {
        try
        {
            await _importFsmHomeOfficeDataUseCase.Execute(file);
            return new ObjectResult(new MessageResponse { Data = $"{file.FileName} - {Admin.HomeOfficeFileProcessed}" })
            { StatusCode = StatusCodes.Status200OK };
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Truncates FsmHMRCData and imports a new data set from XML input
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/admin/import-hmrc-data")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> ImportFsmHMRCData(IFormFile file)
    {
        try
        {
            await _importFsmHMRCDataUseCase.Execute(file);
            return new ObjectResult(new MessageResponse { Data = $"{file.FileName} - {Admin.HMRCFileProcessed}" })
            { StatusCode = StatusCodes.Status200OK };
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }
    
    /// <summary>
    ///     Imports a new Working Families data set from macro enabled excel input
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("multipart/form-data")]
    [HttpPost("/admin/import-wf-hmrc-data")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> ImportWfHMRCData(IFormFile file)
    {
        try
        {
            await _importWfHMRCDataUseCase.Execute(file);
            return new ObjectResult(new MessageResponse { Data = $"{file.FileName} - {Admin.HMRCFileProcessed}" })
            { StatusCode = StatusCodes.Status200OK };
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }
}