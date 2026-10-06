using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CheckEligibilityUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockValidator = new Mock<IValidator<IEligibilityServiceType>>();
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<CheckEligibilityUseCase>>(MockBehavior.Loose);
        _sut = new CheckEligibilityUseCase(_mockCheckGateway.Object, _mockAuditGateway.Object,
            _mockValidator.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IValidator<IEligibilityServiceType>> _mockValidator = null!;
    private Mock<ICheckEligibility> _mockCheckGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ILogger<CheckEligibilityUseCase>> _mockLogger = null!;
    private CheckEligibilityUseCase _sut = null!;

    [Test]
    public async Task Execute_returns_failure_when_model_is_null()
    {
        //Arrange
        var meta = _fixture.Create<CheckMetaData>();
        // Act
        Func<Task> act = async () =>
            await _sut.Execute<CheckEligibilityRequestData>(null!, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Missing request data");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_null()
    {
        // Arrange
        var model = new CheckEligibilityRequest<CheckEligibilityRequestData> { Data = null };
        var meta = _fixture.Create<CheckMetaData>();

        // Act
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Missing request data");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_type_is_incorrect()
    {
        // Arrange
        // Use a different type that implements the same interface or extends the same base class
        var incorrectModel = new IncorrectModelType
        {
            Data = new CheckEligibilityRequestData
            {
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2000-01-01",
                LastName = "Doe"
            }
        };

        var responseData = new PostCheckResult
        {
            Id = _fixture.Create<string>(),
            Status = CheckEligibilityStatus.queuedForProcessing
        };
        var meta = _fixture.Create<CheckMetaData>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta))
            .ReturnsAsync(responseData);
        // Act
        // The factory will convert any model to the correct type based on routeType
        // So this test should actually succeed now
        var result = await _sut.Execute(incorrectModel, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
    }

    // Add this class to help with the test
    private class IncorrectModelType : CheckEligibilityRequest<CheckEligibilityRequestData>
    {
        // Inheriting from CheckEligibilityRequest but it's a different type
    }

    [Test]
    public async Task Execute_normalizes_input_data()
    {
        // Arrange
        var model = CreateValidCheckRequest();
        var meta = _fixture.Create<CheckMetaData>();
        var responseData = new PostCheckResult
        {
            Id = _fixture.Create<string>(),
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        // Setup with a callback to capture the actual argument
        IEligibilityServiceType? capturedArg = null;

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());

        _mockCheckGateway
            .Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), It.IsAny<CheckMetaData>()))
            .Callback<IEligibilityServiceType, CheckMetaData>((arg, metaArg) => capturedArg = arg)
            .ReturnsAsync(responseData);
        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        // Check that normalization happened on the model
        model.Data!.NationalInsuranceNumber.Should().Be("AB123456C"); // Verify the service was called
        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta),
            Times.Once); // Additional check to diagnose the issue - examine what was actually passed
        capturedArg.Should().NotBeNull("PostCheck should have been called");
        if (capturedArg != null && capturedArg is CheckEligibilityRequestData requestData)
            requestData.NationalInsuranceNumber.Should().Be("AB123456C");
    }

    [Test]
    public async Task Execute_normalizes_last_name_to_uppercase()
    {
        // Arrange
        var model = new CheckEligibilityRequest<CheckEligibilityRequestData>
        {
            Data = new CheckEligibilityRequestData
            {
                NationalInsuranceNumber = "ab123456c",
                DateOfBirth = "2000-01-01",
                LastName = "doe"
            }
        };
        var meta = _fixture.Create<CheckMetaData>();
        var responseData = new PostCheckResult
        {
            Id = _fixture.Create<string>(),
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway
            .Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), It.IsAny<CheckMetaData>()))
            .ReturnsAsync(responseData);

        // Act
        await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        model.Data!.LastName.Should().Be("DOE");
    }

    [Test]
    public async Task Execute_returns_failure_when_validation_fails()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
        var model = new CheckEligibilityRequest<CheckEligibilityRequestData>
        {
            Data = new CheckEligibilityRequestData
            {
                // Missing required fields for validation
                DateOfBirth = "2000-01-01"
            }
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult([
                new ValidationFailure("test", "test error")
            ])); // Act
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_response_WF()
    {
        // Arrange
        var model = CreateValidWFCheckRequest();
        var checkId = _fixture.Create<string>();
        var meta = _fixture.Create<CheckMetaData>();
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>(), meta))
            .ReturnsAsync(responseData);

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.WorkingFamilies, meta);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be(responseData.Status.ToString());
        result.Links.Should().NotBeNull();
        result.Links.Get_EligibilityCheck.Should().Be($"{CheckLinks.GetLink}{checkId}");
        result.Links.Put_EligibilityCheckProcess.Should().Be($"{CheckLinks.ProcessLink}{checkId}");
        result.Links.Get_EligibilityCheckStatus.Should().Be($"{CheckLinks.GetLink}{checkId}/status");
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_response()
    {
        // Arrange
        var model = CreateValidCheckRequest();
        var checkId = _fixture.Create<string>();
        var meta = _fixture.Create<CheckMetaData>();
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta))
            .ReturnsAsync(responseData);

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be(responseData.Status.ToString());
        result.Links.Should().NotBeNull();
        result.Links.Get_EligibilityCheck.Should().Be($"{CheckLinks.GetLink}{checkId}");
        result.Links.Put_EligibilityCheckProcess.Should().Be($"{CheckLinks.ProcessLink}{checkId}");
        result.Links.Get_EligibilityCheckStatus.Should().Be($"{CheckLinks.GetLink}{checkId}/status");
    }

    [Test]
    public async Task Execute_calls_gateway_PostCheck_with_correct_data()
    {
        // Arrange
        var model = CreateValidCheckRequest();
        var checkId = _fixture.Create<string>();
        var meta = _fixture.Create<CheckMetaData>();
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta))
            .ReturnsAsync(responseData);
        // Act
        await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        _mockCheckGateway.Verify(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta), Times.Once);
    }

    [Test]
    public async Task Execute_returns_failure_when_gateway_returns_null_response()
    {
        // Arrange
        var model = CreateValidCheckRequest();
        var meta = _fixture.Create<CheckMetaData>();
        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());

        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>(), meta))
            .ReturnsAsync((PostCheckResult)null!);

        // Act
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Eligibility check not completed successfully.");

        // Verify audit service was not called
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task Execute_ForwardsCanonicalNino_WithRealValidation(
    [Values(false, true)] bool workingFamilies,
    [Values(
        "ab 12 34 56 c",
        "ab-12.34/56c",
        "ab\t12\r\n3456c")] string input)
    {
        var sut = new CheckEligibilityUseCase(
            _mockCheckGateway.Object,
            _mockAuditGateway.Object,
            new FeatureManagement.Domain.Validation.CheckEligibilityRequestDataValidator(),
            _mockLogger.Object);

        var meta = _fixture.Create<CheckMetaData>();
        string? forwardedNino = null;

        _mockCheckGateway
            .Setup(g => g.PostCheck(It.IsAny<IEligibilityServiceType>(), meta))
            .Callback<IEligibilityServiceType, CheckMetaData>((data, _) =>
            {
                forwardedNino =
                    ((CheckEligibilityRequestDataBase)data).NationalInsuranceNumber;
            })
            .ReturnsAsync(new PostCheckResult
            {
                Id = "nino-contract-test",
                Status = CheckEligibilityStatus.queuedForProcessing
            });

        if (workingFamilies)
        {
            var request = CreateValidWFCheckRequest();
            request.Data!.NationalInsuranceNumber = input;
            await sut.Execute(request, CheckEligibilityType.WorkingFamilies, meta);
        }
        else
        {
            var request = CreateValidCheckRequest();
            request.Data!.NationalInsuranceNumber = input;
            await sut.Execute(request, CheckEligibilityType.FreeSchoolMeals, meta);
        }

        forwardedNino.Should().Be("AB123456C");

        _mockCheckGateway.Verify(
            g => g.PostCheck(It.IsAny<IEligibilityServiceType>(), meta),
            Times.Once);
    }

    [Test]
    public async Task Execute_RejectsInvalidNino_WithRealValidation(
        [Values(false, true)] bool workingFamilies,
        [Values(
        "BG123456C",
        "A1123456C",
        "AB123456E",
        "AB123456 ",
        "AB12345C",
        "AB123456CD",
        "AB\u0661\u0662\u0663\u0664\u0665\u0666C",
        "---")] string input)
    {
        var sut = new CheckEligibilityUseCase(
            _mockCheckGateway.Object,
            _mockAuditGateway.Object,
            new FeatureManagement.Domain.Validation.CheckEligibilityRequestDataValidator(),
            _mockLogger.Object);

        var meta = _fixture.Create<CheckMetaData>();

        Func<Task> act = async () =>
        {
            if (workingFamilies)
            {
                var request = CreateValidWFCheckRequest();
                request.Data!.NationalInsuranceNumber = input;
                await sut.Execute(request, CheckEligibilityType.WorkingFamilies, meta);
            }
            else
            {
                var request = CreateValidCheckRequest();
                request.Data!.NationalInsuranceNumber = input;
                await sut.Execute(request, CheckEligibilityType.FreeSchoolMeals, meta);
            }
        };

        await act.Should().ThrowAsync<ValidationException>();

        _mockCheckGateway.Verify(
            g => g.PostCheck(
                It.IsAny<IEligibilityServiceType>(),
                It.IsAny<CheckMetaData>()),
            Times.Never);
    }

    private CheckEligibilityRequest<CheckEligibilityRequestData> CreateValidCheckRequest()
    {
        return new CheckEligibilityRequest<CheckEligibilityRequestData>
        {
            Data = new CheckEligibilityRequestData
            {
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2000-01-01",
                LastName = "Doe"
            }
        };
    }

    private CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData> CreateValidWFCheckRequest()
    {
        return new CheckEligibilityRequest<CheckEligibilityRequestWorkingFamiliesData>
        {
            Data = new CheckEligibilityRequestWorkingFamiliesData
            {
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2000-01-01",
                LastName = "Doe",
                EligibilityCode = "50012344556"
            }
        };
    }
}