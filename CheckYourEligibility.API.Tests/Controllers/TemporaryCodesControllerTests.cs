using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Validation;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests.Controllers
{
	[TestFixture]
	public class TemporaryCodesControllerTests
	{
		protected readonly Fixture _fixture = new();

		private Mock<ILogger<TemporaryCodesController>> _loggerMock = null;
		private Mock<IAudit> _auditMock = null;
		private Mock<ITemporaryCodeUploadUseCase> _temporaryCodeUploadUseCaseMock = null;
		private Mock<IGetTemporaryCodeUploadsUseCase> _getTemporaryCodeUploadsUseCaseMock = null;

		private IConfigurationRoot _configuration = null!;
		private TemporaryCodesController _sut = null!;

		private void SetupControllerUser()
		{
			var httpContext = new DefaultHttpContext();

			httpContext.User = new ClaimsPrincipal(
				new ClaimsIdentity(
				[
					new Claim(
				JwtClaimTypes.UserId,
				Guid.NewGuid().ToString())
				]));

			_sut.ControllerContext = new ControllerContext
			{
				HttpContext = httpContext
			};
		}

		[SetUp]
		public void Setup()
		{
			_temporaryCodeUploadUseCaseMock = new Mock<ITemporaryCodeUploadUseCase>(MockBehavior.Strict);
			_configuration = new ConfigurationBuilder().Build();
			_getTemporaryCodeUploadsUseCaseMock = new Mock<IGetTemporaryCodeUploadsUseCase>(MockBehavior.Strict);
			_auditMock = new Mock<IAudit>(MockBehavior.Strict);
			_loggerMock = new Mock<ILogger<TemporaryCodesController>>();

			var configData = new Dictionary<string, string?>
			{
				{ "Jwt:Scopes:user", "user" },
				{ "Jwt:Scopes:admin", "admin" },
				{ "Jwt:Scopes:support", "support" }
			};
			_configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

			_sut = new TemporaryCodesController(
				_loggerMock.Object,
				_auditMock.Object,
				_configuration,
				_temporaryCodeUploadUseCaseMock.Object,
				_getTemporaryCodeUploadsUseCaseMock.Object
			);

			SetupControllerUser();
		}

		[TearDown]
		public void TearDown()
		{
			_temporaryCodeUploadUseCaseMock.VerifyAll();
			_getTemporaryCodeUploadsUseCaseMock.VerifyAll();
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Return_Ok_When_UseCase_Succeeds()
		{
			// Arrange
			var response = new TemporaryCodeUploadSearchResponse();

			_getTemporaryCodeUploadsUseCaseMock
				.Setup(x => x.Execute(It.IsAny<TemporaryCodeUploadsSearchRequest>()))
				.ReturnsAsync(response);

			// Act
			var result = await _sut.GetTemporaryCodeUploads(1, 10);

			// Assert
			var objectResult = result as OkObjectResult;

			objectResult.Should().NotBeNull();
			objectResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
			objectResult.Value.Should().Be(response);
		}

		[Test]
		public async Task TemporaryCodesUpload_Should_Return_Accepted_When_UseCase_Succeeds()
		{
			// Arrange
			var fileMock = new Mock<IFormFile>();

			var request = new TemporaryCodeUploadCreateRequest
			{
				File = fileMock.Object
			};

			var response = new TemporaryCodeUploadCreateResponse
			{
				NumberOfCodes = 2,
				Errors = new List<ImportTempCodeValidationError>()
			};

			_temporaryCodeUploadUseCaseMock
				.Setup(x => x.Execute(
					request,
					It.IsAny<string>()))
				.ReturnsAsync(response);

			// Act
			var result = await _sut.TemporaryCodesUpload(request);

			// Assert
			var acceptedResult = result.Should()
				.BeOfType<AcceptedResult>()
				.Subject;

			acceptedResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
			acceptedResult.Value.Should().Be(response);
		}

		[Test]
		public async Task TemporaryCodesUpload_Should_Return_BadRequest_When_BulkTempCodeValidationException_Is_Thrown()
		{
			// Arrange
			var fileMock = new Mock<IFormFile>();

			var request = new TemporaryCodeUploadCreateRequest
			{
				File = fileMock.Object
			};

			var errors = new List<ImportTempCodeValidationError>
			{
				new()
				{
					RowNumber = 2,
					EligibilityCode = "50173110190",
					ErrorMessages =
					[
						"Eligibility code already exists"
					]
				}
			};

			_temporaryCodeUploadUseCaseMock
				.Setup(x => x.Execute(
					request,
					It.IsAny<string>()))
				.ThrowsAsync(
					new BulkTempCodeValidationException(errors));

			// Act
			var result = await _sut.TemporaryCodesUpload(request);

			// Assert
			var badRequestResult = result.Should()
				.BeOfType<BadRequestObjectResult>()
				.Subject;

			badRequestResult.StatusCode.Should()
				.Be(StatusCodes.Status400BadRequest);

			var response = badRequestResult.Value as TemporaryCodeUploadCreateResponse;

			response.Should().NotBeNull();
			response!.NumberOfCodes.Should().Be(0);
			response.Errors.Should().BeEquivalentTo(errors);
		}
	}
}