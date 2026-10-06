using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CheckEligibilityBulkUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockValidator = new Mock<IValidator<IEligibilityServiceType>>();
        _mockCheckGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _mockBulkCheckGateway = new Mock<IBulkCheck>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<CheckEligibilityBulkUseCase>>(MockBehavior.Loose);
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ICheckEligibility))).Returns(_mockCheckGateway.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        _sut = new CheckEligibilityBulkUseCase(
            _mockValidator.Object,
            _mockCheckGateway.Object,
            _mockBulkCheckGateway.Object,
            _mockAuditGateway.Object,
            _mockLogger.Object,
            _mockScopeFactory.Object);
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
    private Mock<IBulkCheck> _mockBulkCheckGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<CheckEligibilityBulkUseCase>> _mockLogger;
    private Mock<IServiceScopeFactory> _mockScopeFactory;
    private CheckEligibilityBulkUseCase _sut;
    private int _recordCountLimit;

    [Test]
    public async Task Execute_returns_failure_when_model_data_is_null()
    {
        // Arrange
        var model = new CheckEligibilityRequestBulk { Data = null };
        var meta = _fixture.Create<CheckMetaData>();
        // Act
        Func<Task> act = async () =>
            await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid Request, data is required.");
    }

    [Test]
    public async Task Execute_returns_failure_when_record_count_exceeds_limit()
    {
        // Arrange
        var limit = 5;
        var data = _fixture.CreateMany<CheckEligibilityRequestBulkData>(limit + 1).ToList();
        var model = new CheckEligibilityRequestBulk { Data = data };
        var meta = _fixture.Create<CheckMetaData>();

        // Act
        Func<Task> act = async () =>
            await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, limit, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage($"Invalid Request, data limit of {limit} exceeded, {data.Count} records.");
    }

    [Test]
    public async Task Execute_returns_failure_when_validation_errors_exist()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
        // Create a request with invalid data that will fail validation
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new() // Empty properties will fail validation
            {
                DateOfBirth = "1990-01-01"
            }
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        _mockValidator
            .Setup(v => v.Validate(It.IsAny<IEligibilityServiceType>()))
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("LastName", "Invalid last name")
            }));

        // Act
        Func<Task> act = async () =>
            await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_calls_gateways_with_correct_parameters_when_valid_WF()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
        var data = new List<CheckEligibilityRequestWorkingFamiliesBulkData>
        {
            new()
            {
                EligibilityCode = "50012345678",
                DateOfBirth = "2023-01-01",
                LastName = "Smith",
                NationalInsuranceNumber = "AB123456C"
            }
        };
        var model = new CheckEligibilityRequestWorkingFamiliesBulk { Data = data, Meta = { } };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestWorkingFamiliesData>()))
            .Returns(new ValidationResult());

        _mockBulkCheckGateway.Setup(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .ReturnsAsync(_fixture.Create<string>());
        var postCheckCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockCheckGateway.Setup(s =>
                s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta))
            .Callback(() => postCheckCalled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.WorkingFamilies, _recordCountLimit, meta);
        await postCheckCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Data.Status.Should().Be(Messages.Processing);
        result.Links.Should().NotBeNull();
        result.Links.Get_Progress_Check.Should().Contain(CheckLinks.BulkCheckProgress);
        result.Links.Get_BulkCheck_Results.Should().Contain(CheckLinks.BulkCheckResults);

        _mockBulkCheckGateway.Verify(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Once);
        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta), Times.Once);
    }

    [Test]
    public async Task Execute_calls_gateways_with_correct_parameters_when_valid()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
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
            .Returns(new ValidationResult());

        _mockBulkCheckGateway.Setup(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .ReturnsAsync(_fixture.Create<string>());
        var postCheckCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockCheckGateway.Setup(s =>
                s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta))
            .Callback(() => postCheckCalled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);
        await postCheckCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.Data.Status.Should().Be(Messages.Processing);
        result.Links.Should().NotBeNull();
        result.Links.Get_Progress_Check.Should().Contain(CheckLinks.BulkCheckProgress);
        result.Links.Get_BulkCheck_Results.Should().Contain(CheckLinks.BulkCheckResults);

        _mockBulkCheckGateway.Verify(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Once);
        _mockCheckGateway.Verify(
            s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta), Times.Once);
    }

    [TestCase("ab 12 34 56 c")]
    [TestCase("ab-12.34/56c")]
    [TestCase("ab\t12\r\n3456c")]
    public async Task Execute_ValidatesOriginalNino_AndForwardsCanonicalNino(
    string input)
    {
        var meta = _fixture.Create<CheckMetaData>();
        var model = new CheckEligibilityRequestBulk
        {
            Data = new List<CheckEligibilityRequestBulkData>
            {
                new CheckEligibilityRequestBulkData
                {
                    LastName = "Smith",
                    DateOfBirth = "1990-01-01",
                    NationalInsuranceNumber = input
                }
            }
        };

        _mockValidator
            .Setup(v => v.Validate(It.IsAny<IEligibilityServiceType>()))
            .Callback<IEligibilityServiceType>(data =>
                ((CheckEligibilityRequestDataBase)data)
                    .NationalInsuranceNumber.Should().Be(input))
            .Returns(new ValidationResult());

        _mockBulkCheckGateway
            .Setup(g => g.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .ReturnsAsync("bulk-check-id");

        var posted = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _mockCheckGateway
            .Setup(g => g.PostCheck(
                It.IsAny<IEnumerable<IEligibilityServiceType>>(),
                It.IsAny<string>(),
                meta))
            .Callback<IEnumerable<IEligibilityServiceType>, string, CheckMetaData>(
                (data, _, _) =>
                {
                    posted.TrySetResult(
                        ((CheckEligibilityRequestDataBase)data.Single())
                            .NationalInsuranceNumber);
                })
            .Returns(Task.CompletedTask);

        await _sut.Execute(
            model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);

        (await posted.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be("AB123456C");

        _mockBulkCheckGateway.Verify(
            g => g.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Once);
    }

    [Test]
    public async Task Execute_sets_FinalNameInCheck_to_empty_string_when_LastName_Of_LastRecord_is_null()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new() { LastName = null, DateOfBirth = "1990-01-01", NationalInsuranceNumber = "AB123456C" },
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());

        BulkCheck capturedBulkCheck = null;
        _mockBulkCheckGateway.Setup(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .Callback<BulkCheck>(b => capturedBulkCheck = b)
            .ReturnsAsync("bulk-check-id");
        var postCheckCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta))
            .Callback(() => postCheckCalled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);
        await postCheckCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        capturedBulkCheck.Should().NotBeNull();
        capturedBulkCheck.FinalNameInCheck.Should().Be("");
    }

    [Test]
    public async Task Execute_sets_FinalNameInCheck_to_uppercase()
    {
        // Arrange
        var meta = _fixture.Create<CheckMetaData>();
        var data = new List<CheckEligibilityRequestBulkData>
        {
            new() { LastName = "smith", DateOfBirth = "1991-01-01", NationalInsuranceNumber = "AB123456C" },
        };
        var model = new CheckEligibilityRequestBulk { Data = data };

        _mockValidator.Setup(v => v.Validate(It.IsAny<CheckEligibilityRequestData>()))
            .Returns(new ValidationResult());

        BulkCheck capturedBulkCheck = null;
        _mockBulkCheckGateway.Setup(s => s.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .Callback<BulkCheck>(b => capturedBulkCheck = b)
            .ReturnsAsync("bulk-check-id");
        var postCheckCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockCheckGateway.Setup(s => s.PostCheck(It.IsAny<IEnumerable<IEligibilityServiceType>>(), It.IsAny<string>(), meta))
            .Callback(() => postCheckCalled.SetResult(true))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(model, CheckEligibilityType.FreeSchoolMeals, _recordCountLimit, meta);
        await postCheckCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        capturedBulkCheck.Should().NotBeNull();
        capturedBulkCheck.FinalNameInCheck.Should().Be("SMITH");
    }

    [Test]
    public async Task Execute_rejects_forbidden_nino_prefix_before_creating_bulk_check()
    {
        const string submittedNino = "bg-12.34/56c";
        var validator = new FeatureManagement.Domain.Validation.CheckEligibilityRequestDataValidator();
        _sut = new CheckEligibilityBulkUseCase(
            validator,
            _mockCheckGateway.Object,
            _mockBulkCheckGateway.Object,
            _mockAuditGateway.Object,
            _mockLogger.Object,
            _mockScopeFactory.Object);

        var item = new CheckEligibilityRequestBulkData
        {
            LastName = "Smith",
            DateOfBirth = "1990-01-01",
            NationalInsuranceNumber = submittedNino
        };
        var model = new CheckEligibilityRequestBulk
        {
            Data = new List<CheckEligibilityRequestBulkData> { item }
        };

        validator.Validate((IEligibilityServiceType)item).Errors
            .Should().Contain(e =>
                e.ErrorMessage ==
                CheckYourEligibility.API.Domain.Constants.ErrorMessages.ValidationMessages.NI);

        Func<Task> act = () => _sut.Execute(
            model,
            CheckEligibilityType.FreeSchoolMeals,
            _recordCountLimit,
            _fixture.Create<CheckMetaData>());

        await act.Should()
            .ThrowAsync<CheckYourEligibility.API.Domain.Exceptions.ValidationException>();

        item.NationalInsuranceNumber.Should().Be(submittedNino);
        _mockBulkCheckGateway.Verify(
            g => g.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Never);
        _mockScopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}

public class DerivedCheckEligibilityRequestBulk : CheckEligibilityRequestBulk
{
}