using System;
using System.Threading;
using System.Threading.Tasks;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateWorkingFamiliesCorrectionUseCaseTests
{
    private const string EligibilityCode = "50000375201";
    private const string ExpectedVersion = "version-from-lookup";

    private Mock<IWorkingFamiliesCorrection> _gateway = null!;
    private UpdateWorkingFamiliesCorrectionUseCase _useCase = null!;
    private WorkingFamiliesChildCorrectionRequest _childRequest = null!;
    private WorkingFamiliesAdultCorrectionRequest _adultRequest = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IWorkingFamiliesCorrection>(MockBehavior.Strict);
        _useCase = new UpdateWorkingFamiliesCorrectionUseCase(_gateway.Object);

        _childRequest = new WorkingFamiliesChildCorrectionRequest
        {
            FirstName = "Sam",
            LastName = "Child",
            DateOfBirth = "2021-06-15",
            Postcode = "SW1A 1AA",
            ExpectedVersion = ExpectedVersion
        };

        _adultRequest = new WorkingFamiliesAdultCorrectionRequest
        {
            FirstName = "Alex",
            LastName = "Carer",
            DateOfBirth = "1985-05-10",
            NationalInsuranceNumber = "ab 12 34 56 c",
            ExpectedVersion = ExpectedVersion
        };
    }

    #region Tests

    [TestCase("child", " 50000375201 ", "50000375201")]
    [TestCase("carer", " 50000375201 ", "50000375201")]
    [TestCase("partner", " 50000375201 ", "50000375201")]
    [TestCase("child", "10000375201", "10000375201")]
    [TestCase("carer", "10000375201", "10000375201")]
    [TestCase("partner", "10000375201", "10000375201")]
    public async Task Valid_request_calls_correct_gateway_operation(
        string section,
        string enteredCode,
        string expectedCode)
    {
        using var cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;
        var record = CreateRecord(expectedCode);

        SetupSuccessfulUpdate(section, expectedCode, token, record);

        var result = await Execute(section, enteredCode, token);

        result.Data.Should().BeSameAs(record);

        _childRequest.ExpectedVersion.Should().Be(ExpectedVersion);
        _adultRequest.ExpectedVersion.Should().Be(ExpectedVersion);

        _gateway.VerifyAll();
        _gateway.Invocations.Should().ContainSingle();
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Invalid_fields_prevent_gateway_call(string section)
    {
        _childRequest.FirstName = "";
        _adultRequest.FirstName = "";

        Func<Task> act = () => Execute(section, EligibilityCode);

        var exception = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        exception.Which.Errors.Should()
            .ContainSingle()
            .Which.PropertyName.Should().Be("FirstName");

        _gateway.VerifyNoOtherCalls();
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Foster_code_prevents_gateway_call(string section)
    {
        Func<Task> act = () => Execute(section, " 40000000001 ");

        await act.Should()
            .ThrowAsync<FosterCodeCorrectionNotSupportedException>();

        _gateway.VerifyNoOtherCalls();
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Invalid_code_prevents_gateway_call(string section)
    {
        Func<Task> act = () => Execute(section, "invalid-code");

        await act.Should()
            .ThrowAsync<
                CheckYourEligibility.API.Domain.Exceptions.ValidationException>();

        _gateway.VerifyNoOtherCalls();
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Missing_request_body_prevents_gateway_call(string section)
    {
        Func<Task> act = () => Execute(
            section,
            EligibilityCode,
            missingBody: true);

        var exception = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        exception.Which.Errors.Should()
            .ContainSingle()
            .Which.PropertyName.Should().Be("request");

        _gateway.VerifyNoOtherCalls();
    }

    [TestCase("child")]
    [TestCase("carer")]
    [TestCase("partner")]
    public async Task Missing_expected_version_prevents_gateway_call(string section)
    {
        _childRequest.ExpectedVersion = string.Empty;
        _adultRequest.ExpectedVersion = string.Empty;

        Func<Task> act = () => Execute(section, EligibilityCode);

        var exception = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        exception.Which.Errors.Should()
            .ContainSingle()
            .Which.PropertyName.Should().Be("ExpectedVersion");

        _gateway.VerifyNoOtherCalls();
    }

    #endregion

    #region Helpers

    private Task<WorkingFamiliesCorrectionResponse> Execute(
        string section,
        string? eligibilityCode,
        CancellationToken cancellationToken = default,
        bool missingBody = false)
    {
        return section switch
        {
            "child" => _useCase.UpdateChild(
                eligibilityCode,
                missingBody ? null : _childRequest,
                cancellationToken),

            "carer" => _useCase.UpdateCarer(
                eligibilityCode,
                missingBody ? null : _adultRequest,
                cancellationToken),

            "partner" => _useCase.UpdatePartner(
                eligibilityCode,
                missingBody ? null : _adultRequest,
                cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
    }

    private void SetupSuccessfulUpdate(
        string section,
        string code,
        CancellationToken token,
        WorkingFamiliesCorrectionRecord record)
    {
        switch (section)
        {
            case "child":
                _gateway
                    .Setup(x => x.UpdateChild(code, _childRequest, token))
                    .ReturnsAsync(record);
                break;

            case "carer":
                _gateway
                    .Setup(x => x.UpdateCarer(code, _adultRequest, token))
                    .ReturnsAsync(record);
                break;

            case "partner":
                _gateway
                    .Setup(x => x.UpdatePartner(code, _adultRequest, token))
                    .ReturnsAsync(record);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
    }

    private static WorkingFamiliesCorrectionRecord CreateRecord(string code)
    {
        return new WorkingFamiliesCorrectionRecord
        {
            EligibilityCode = code,
            SummaryId = "summary-id",
            LatestEventId = "event-id",
            Version = "updated-version",
            Child = new WorkingFamiliesCorrectionChild
            {
                FirstName = "Sam",
                LastName = "Child",
                DateOfBirth = new DateTime(2021, 6, 15),
                Postcode = "SW1A 1AA"
            },
            Carer = new WorkingFamiliesCorrectionAdult
            {
                FirstName = "Alex",
                LastName = "Carer",
                DateOfBirth = new DateTime(1985, 5, 10),
                NationalInsuranceNumber = "AB123456C"
            }
        };
    }

    #endregion
}