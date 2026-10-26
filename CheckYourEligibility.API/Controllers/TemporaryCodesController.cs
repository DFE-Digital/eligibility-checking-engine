using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CheckYourEligibility.API.Controllers
{
	[ApiController]
	[Route("[controller]")]
	[Authorize]
	public class TemporaryCodesController : BaseController
	{
		private readonly ILogger<TemporaryCodesController> _logger;
		private readonly ITemporaryCodeUploadUseCase _temporaryCodeUploadUseCase;
		private readonly IGetTemporaryCodeUploadsUseCase _getTemporaryCodeUploadsUseCase;

		public TemporaryCodesController(
			ILogger<TemporaryCodesController> logger,
			IAudit audit,
			IConfiguration configuration,
			ITemporaryCodeUploadUseCase temporaryCodeUploadUseCase,
			IGetTemporaryCodeUploadsUseCase getTemporaryCodeUploadsUseCase
		)
			: base(audit)
		{
			_logger = logger;

			_temporaryCodeUploadUseCase = temporaryCodeUploadUseCase;
			_getTemporaryCodeUploadsUseCase = getTemporaryCodeUploadsUseCase;
		}

		/// <summary>
		/// Temporary codes bulk import from an Excel file.
		/// </summary>
		/// <param name="request">The bulk import request containing binary file</param>
		/// <returns>Import results with success/failure counts and error details</returns>
		[ProducesResponseType(typeof(TemporaryCodeUploadCreateResponse), (int)HttpStatusCode.Accepted)]
		[ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
		[Consumes("multipart/form-data")]
		[HttpPost("/temporary-codes/upload")]
		[Authorize(Policy = PolicyNames.RequireAdminScope)]
		public async Task<ActionResult> TemporaryCodesUpload([FromForm] TemporaryCodeUploadCreateRequest request)
		{
			try
			{
				var response = await _temporaryCodeUploadUseCase.Execute(request, User.GetUserID().ToString());
				return Accepted(response);
			}
			catch (UnauthorizedAccessException ex)
			{
				return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
			}
			catch (BulkTempCodeValidationException ex)
			{
				return BadRequest(new TemporaryCodeUploadCreateResponse
				{
					NumberOfCodes = 0,
					Errors = ex.Errors
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during bulk import");
				return BadRequest(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
			}
		}

		/// <summary>
		///     Get a list of Temporary codes file uploads
		/// </summary>
		/// <returns></returns>
		[ProducesResponseType(typeof(TemporaryCodeUploadResponse), (int)HttpStatusCode.OK)]
		[Consumes("application/json", "application/vnd.api+json;version=1.0")]
		[HttpGet("/temporary-codes/search")]
		[Authorize(Policy = PolicyNames.RequireAdminScope)]
		public async Task<ActionResult> GetTemporaryCodeUploads([FromQuery] int pageNumber, [FromQuery] int pageSize)
		{
			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = pageNumber,
				PageSize = pageSize
			};

			var response = await _getTemporaryCodeUploadsUseCase.Execute(request);

			return Ok(response);
		}
	}
}
