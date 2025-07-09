using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CheckEligibilityBulkUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockValidator = new Mock<IValidator<IEligibilityServiceType>>();
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<CheckEligibilityBulkUseCase>>(MockBehavior.Loose);
        _sut = new CheckEligibilityBulkUseCase(
            _mockValidator.Object,
            _mockCheckGateway.Object, 
            _mockAuditGateway.Object, 
            _mockLogger.Object);
        _fixture = new Fixture();
        _recordCountLimit = 100;
    }

    [TearDown]
    public void Teardown()
    {
        _mockCheckGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }


    private Mock<IValidator<IEligibilityServiceType>> _mockValidator;
    private Mock<ICheckEligibility> _mockCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<CheckEligibilityBulkUseCase>> _mockLogger;
    private CheckEligibilityBulkUseCase _sut;
    private Fixture _fixture;
    private int _recordCountLimit;

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_null()
    {
        // Arrange
        var model = new CheckEligibilityRequestBulk { Data = null };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, _recordCountLimit);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, data is required.");
    }

    [Test]
    public async Task Execute_returns_failure_when_record_count_exceeds_limit()
    {
        // Arrange
        var limit = 5;
        var data = _fixture.CreateMany<CheckEligibilityRequestBulkData>(limit + 1).ToList();
        var model = new CheckEligibilityRequestBulk { Data = data };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, limit);

        // Assert
        act.Should().ThrowAsync<ValidationException>()
            .WithMessage($"Invalid Request, data limit of {limit} exceeded, {data.Count} records.");
    }

    [Test]
    public async Task Execute_returns_failure_when_validation_errors_exist()
    {
        // Arrange
        // Create a request with invalid data that will fail validation
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new() // Empty properties will fail validation
            {
                DateOfBirth = "1990-01-01"
            }
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, _recordCountLimit);

        // Assert
        act.Should().ThrowAsync<ValidationException>();
    }
    [Test]
    public async Task Execute_calls_gateways_with_correct_parameters_when_valid_WF()
    {
        // Arrange
        var data = new List<CheckEligibilityRequestWorkingFamiliesBulkData>
        {
            new()
            {
                EligibilityCode = "50012345678",
                ChildDateOfBirth = "2023-01-01",
                NationalInsuranceNumber = "AB123456C"
            }
        };
        var model = new CheckEligibilityRequestWorkingFamiliesBulk { Data = data };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mockCheckGateway.Setup(s =>
                s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<string>());


        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.WorkingFamilies, _recordCountLimit);

        // Assert
        result.Data.Status.Should().Be(Messages.Processing);
        result.Links.Should().NotBeNull();
        result.Links.Get_Progress_Check.Should().Contain(CheckLinks.BulkCheckProgress);
        result.Links.Get_BulkCheck_Results.Should().Contain(CheckLinks.BulkCheckResults);

        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>()), Times.Once);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()), Times.Once);
    }
    [Test]
    public async Task Execute_calls_gateways_with_correct_parameters_when_valid()
    {
        // Arrange
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new()
            {
                LastName = "Smith",
                DateOfBirth = "1990-01-01",
                NationalInsuranceNumber = "AB123456C"
            }
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mockCheckGateway.Setup(s =>
                s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<string>());


        // Act
        var result = await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, _recordCountLimit);

        // Assert
        result.Data.Status.Should().Be(Messages.Processing);
        result.Links.Should().NotBeNull();
        result.Links.Get_Progress_Check.Should().Contain(CheckLinks.BulkCheckProgress);
        result.Links.Get_BulkCheck_Results.Should().Contain(CheckLinks.BulkCheckResults);

        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>()), Times.Once);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Execute_converts_national_insurance_number_to_uppercase()
    {
        // Arrange
        var nino = "ab123456c";
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new()
            {
                LastName = "Smith",
                DateOfBirth = "1990-01-01",
                NationalInsuranceNumber = nino
            }
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new FluentValidation.Results.ValidationResult());

        _mockCheckGateway.Setup(s => s.PostCheck(It.Is<IEnumerable<CheckEligibilityRequestData>>(
                d => d.First().NationalInsuranceNumber == nino.ToUpper()), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, _recordCountLimit);

        // Assert
        _mockCheckGateway.Verify(s => s.PostCheck(It.Is<IEnumerable<CheckEligibilityRequestData>>(
            d => d.First().NationalInsuranceNumber == "AB123456C"), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Execute_returns_failure_when_model_type_is_not_expected()
    {
        // Arrange
        // Create a derived class to simulate wrong type
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new()
            {
                LastName = "Smith",
                DateOfBirth = "1990-01-01",
                NationalInsuranceNumber = "AB123456C"
            }
        };

        // Create an instance of the derived class
        var model = new DerivedCheckEligibilityRequestBulk { Data = data };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, Domain.Enums.CheckEligibilityType.FreeSchoolMeals, _recordCountLimit);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage($"Unknown request type:-{model.GetType()}");

        // Verify no services were called
        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEnumerable<CheckEligibilityRequestData>>(), It.IsAny<string>()),
            Times.Never);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.BulkCheck, It.IsAny<string>()), Times.Never);
    }
}

public class DerivedCheckEligibilityRequestBulk : CheckEligibilityRequestBulk
{
}