using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests.Controllers;

[TestFixture]
public class WorkingFamiliesCorrectionControllerTests
{
    private const string Code = "50000375201";

    private Mock<IGetWorkingFamiliesCorrectionUseCase> _useCase = null!;
    private WorkingFamiliesCorrectionController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _useCase = new Mock<IGetWorkingFamiliesCorrectionUseCase>(
            MockBehavior.Strict);

        _controller = new WorkingFamiliesCorrectionController(
            _useCase.Object,
            NullLogger<WorkingFamiliesCorrectionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "correction-test-reference"
                }
            }
        };
    }

    [Test]
    public async Task Successful_lookup_returns_ok_with_response()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var response = CreateResponse();

        _useCase
            .Setup(useCase => useCase.Execute(Code, token))
            .ReturnsAsync(response);

        var result = await _controller.Get(Code, token);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Which;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeSameAs(response);

        _useCase.Verify(
            useCase => useCase.Execute(Code, token),
            Times.Once);
    }

    [Test]
    public async Task Missing_record_returns_not_found()
    {
        _useCase
            .Setup(useCase => useCase.Execute(
                Code, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No record."));

        var result = await _controller.Get(Code, CancellationToken.None);

        var errorResult = result.Result.Should()
            .BeOfType<ObjectResult>().Which;

        errorResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var response = errorResult.Value.Should()
            .BeOfType<ErrorResponse>().Which;

        response.Errors.Should().ContainSingle();
        response.Errors[0].Status.Should().Be(StatusCodes.Status404NotFound);
        response.Errors[0].Title.Should().Be("record_not_found");
    }

    [Test]
    public async Task Invalid_code_returns_bad_request_with_validation_errors()
    {
        var failure = new ValidationException(
            new List<Error>
            {
            new()
            {
                Title = "validation_error",
                Detail = "Enter an 11-digit eligibility code."
            }
            },
            "Invalid eligibility code.");

        _useCase
            .Setup(useCase => useCase.Execute(
                Code, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var result = await _controller.Get(Code, CancellationToken.None);

        var errorResult = result.Result.Should()
            .BeOfType<BadRequestObjectResult>().Which;

        errorResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var response = errorResult.Value.Should()
            .BeOfType<ErrorResponse>().Which;

        response.Errors.Should().ContainSingle();
        response.Errors[0].Status.Should().Be(StatusCodes.Status400BadRequest);
        response.Errors[0].Title.Should().Be("validation_error");
        response.Errors[0].Detail.Should()
            .Be("Enter an 11-digit eligibility code.");
    }

    [Test]
    public async Task Foster_code_returns_unprocessable_entity()
    {
        const string fosterCode = "40000000001";

        _useCase
            .Setup(useCase => useCase.Execute(
                fosterCode, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FosterCodeCorrectionNotSupportedException());

        var result = await _controller.Get(
            fosterCode, CancellationToken.None);

        var errorResult = result.Result.Should()
            .BeOfType<ObjectResult>().Which;

        errorResult.StatusCode.Should()
            .Be(StatusCodes.Status422UnprocessableEntity);

        var response = errorResult.Value.Should()
            .BeOfType<ErrorResponse>().Which;

        response.Errors.Should().ContainSingle();
        response.Errors[0].Status.Should()
            .Be(StatusCodes.Status422UnprocessableEntity);
        response.Errors[0].Title.Should().Be("foster_code_not_supported");
    }

    [Test]
    public async Task Inconsistent_record_returns_conflict_with_safe_details()
    {
        _useCase
            .Setup(useCase => useCase.Execute(
                Code, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkingFamiliesCorrectionDataException(
                "Internal diagnostic information."));

        var result = await _controller.Get(Code, CancellationToken.None);

        var errorResult = result.Result.Should()
            .BeOfType<ObjectResult>().Which;

        errorResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var response = errorResult.Value.Should()
            .BeOfType<ErrorResponse>().Which;

        response.Errors.Should().ContainSingle();
        response.Errors[0].Status.Should().Be(StatusCodes.Status409Conflict);
        response.Errors[0].Title.Should().Be("inconsistent_record");
        response.Errors[0].Detail.Should().Be(
            "Unable to retrieve the record. Reference: correction-test-reference");
    }

    [Test]
    public async Task Unexpected_failure_returns_internal_server_error_with_safe_details()
    {
        var useCase = new Mock<IGetWorkingFamiliesCorrectionUseCase>();

        useCase
            .Setup(x => x.Execute(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Internal database connection details."));

        var controller = new WorkingFamiliesCorrectionController(
            useCase.Object,
            NullLogger<WorkingFamiliesCorrectionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "correction-test-reference"
                }
            }
        };

        var result = await controller.Get(
            "50000375201",
            CancellationToken.None);

        var objectResult = result.Result.Should()
            .BeOfType<ObjectResult>().Which;

        objectResult.StatusCode.Should()
            .Be(StatusCodes.Status500InternalServerError);

        var response = objectResult.Value.Should()
            .BeOfType<ErrorResponse>().Which;

        var error = response.Errors.Should().ContainSingle().Which;

        error.Status.Should().Be(StatusCodes.Status500InternalServerError);
        error.Title.Should().Be("technical_error");
        error.Detail.Should().Be(
            "Unable to retrieve the record. Reference: correction-test-reference");
    }

    [Test]
    public async Task Cancelled_request_propagates_cancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var failure = new OperationCanceledException(
            cancellationSource.Token);

        var useCase = new Mock<IGetWorkingFamiliesCorrectionUseCase>();

        useCase
            .Setup(x => x.Execute(
                "50000375201",
                cancellationSource.Token))
            .ThrowsAsync(failure);

        var controller = new WorkingFamiliesCorrectionController(
            useCase.Object,
            NullLogger<WorkingFamiliesCorrectionController>.Instance);

        Func<Task> action = () => controller.Get(
            "50000375201",
            cancellationSource.Token);

        var exception = await action.Should()
            .ThrowAsync<OperationCanceledException>();

        exception.Which.Should().BeSameAs(failure);
    }

    private static WorkingFamiliesCorrectionResponse CreateResponse()
    {
        return new WorkingFamiliesCorrectionResponse
        {
            Data = new WorkingFamiliesCorrectionRecord
            {
                EligibilityCode = Code,
                SummaryId = "summary-id",
                LatestEventId = "event-id",
                Child = new WorkingFamiliesCorrectionChild
                {
                    FirstName = "Sam",
                    LastName = "Child",
                    DateOfBirth = new DateTime(2021, 6, 15)
                },
                Carer = new WorkingFamiliesCorrectionAdult
                {
                    FirstName = "Alex",
                    LastName = "Carer"
                }
            }
        };
    }
}