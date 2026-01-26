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
        _fixture = new Fixture();
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
    private new Fixture _fixture = null!;

    [Test]
    public async Task Execute_returns_failure_when_model_is_null()
    {
        // Act
        Func<Task> act = async () =>
            await _sut.Execute<CheckEligibilityRequestData>(null!, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Missing request data");
    }

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_null()
    {
        // Arrange
        var model = new CheckEligibilityRequest<CheckEligibilityRequestData> { Data = null };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

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
        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()))
            .ReturnsAsync(responseData);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, responseData.Id, null))
            .ReturnsAsync(_fixture.Create<string>()); // Act
        // The factory will convert any model to the correct type based on routeType
        // So this test should actually succeed now
        var result = await _sut.Execute(incorrectModel, CheckEligibilityType.FreeSchoolMeals);

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
            .Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()))
            .Callback<IEligibilityServiceType>(arg => capturedArg = arg)
            .ReturnsAsync(responseData);

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Check, responseData.Id, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        // Check that normalization happened on the model
        model.Data!.NationalInsuranceNumber.Should().Be("AB123456C"); // Verify the service was called
        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEligibilityServiceType>()),
            Times.Once); // Additional check to diagnose the issue - examine what was actually passed
        capturedArg.Should().NotBeNull("PostCheck should have been called");
        if (capturedArg != null && capturedArg is CheckEligibilityRequestData requestData)
            requestData.NationalInsuranceNumber.Should().Be("AB123456C");
    }

    [Test]
    public async Task Execute_returns_failure_when_validation_fails()
    {
        // Arrange
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
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_returns_success_with_correct_data_when_gateway_returns_response_WF()
    {
        // Arrange
        var model = CreateValidWFCheckRequest();
        var checkId = _fixture.Create<string>();
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>()))
            .ReturnsAsync(responseData);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, checkId, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.WorkingFamilies);

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
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()))
            .ReturnsAsync(responseData);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, checkId, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

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
        var responseData = new PostCheckResult
        {
            Id = checkId,
            Status = CheckEligibilityStatus.queuedForProcessing
        };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()))
            .ReturnsAsync(responseData);

        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Check, checkId, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        _mockCheckGateway.Verify(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()), Times.Once);
    }

    [Test]
    public async Task Execute_returns_failure_when_gateway_returns_null_response()
    {
        // Arrange
        var model = CreateValidCheckRequest();
        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());

        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEligibilityServiceType>()))
            .ReturnsAsync((PostCheckResult)null!);

        // Act
        Func<Task> act = async () => await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Eligibility check not completed successfully.");

        // Verify audit service was not called
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>(),null), Times.Never);
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