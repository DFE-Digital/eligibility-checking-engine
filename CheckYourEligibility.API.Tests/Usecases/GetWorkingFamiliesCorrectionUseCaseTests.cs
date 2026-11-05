using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using CheckYourEligibility.API.Boundary.Responses;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetWorkingFamiliesCorrectionUseCaseTests
{
    private Mock<IWorkingFamiliesCorrection> _gateway = null!;
    private GetWorkingFamiliesCorrectionUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _gateway = new Mock<IWorkingFamiliesCorrection>(MockBehavior.Strict);
        _sut = new GetWorkingFamiliesCorrectionUseCase(_gateway.Object);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1234567890")]
    [TestCase("123456789012")]
    [TestCase("12345A78901")]
    [TestCase("12345 78901")]
    [TestCase("\u06640000000001")]
    public async Task Invalid_code_is_rejected_without_calling_gateway(
        string? code)
    {
        Func<Task> act = () => _sut.Execute(code);

        await act.Should().ThrowAsync<ValidationException>();

        _gateway.Verify(
            gateway => gateway.GetByEligibilityCode(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase("40000000001")]
    [TestCase(" 40000000001 ")]
    public async Task Foster_code_is_rejected_without_calling_gateway(
        string code)
    {
        Func<Task> act = () => _sut.Execute(code);

        await act.Should()
            .ThrowAsync<FosterCodeCorrectionNotSupportedException>();

        _gateway.Verify(
            gateway => gateway.GetByEligibilityCode(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase("50000375201", "50000375201")]
    [TestCase("10000375201", "10000375201")]
    [TestCase(" 50000375201 ", "50000375201")]
    public async Task Valid_code_returns_record_and_passes_normalised_code_to_gateway(
    string input,
    string expectedCode)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var record = CreateRecord(expectedCode);

        _gateway
            .Setup(gateway => gateway.GetByEligibilityCode(expectedCode, token))
            .ReturnsAsync(record);

        var response = await _sut.Execute(input, token);

        response.Data.Should().BeSameAs(record);

        _gateway.Verify(
            gateway => gateway.GetByEligibilityCode(expectedCode, token),
            Times.Once);
    }

    [Test]
    public async Task Missing_record_returns_not_found()
    {
        const string code = "50000375201";

        _gateway
            .Setup(gateway => gateway.GetByEligibilityCode(
                code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkingFamiliesCorrectionRecord?)null);

        Func<Task> act = () => _sut.Execute(code);

        await act.Should().ThrowAsync<NotFoundException>();

        _gateway.Verify(
            gateway => gateway.GetByEligibilityCode(
                code, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Technical_failure_is_propagated()
    {
        const string code = "50000375201";
        var failure = new TimeoutException("Simulated database timeout.");

        _gateway
            .Setup(gateway => gateway.GetByEligibilityCode(
                code, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Func<Task> act = () => _sut.Execute(code);

        var assertion = await act.Should().ThrowAsync<TimeoutException>();
        assertion.Which.Should().BeSameAs(failure);
    }

    [Test]
    public async Task Inconsistent_record_error_is_propagated()
    {
        const string code = "50000375201";
        var failure = new WorkingFamiliesCorrectionDataException(
            "Simulated inconsistent records.");

        _gateway
            .Setup(gateway => gateway.GetByEligibilityCode(
                code, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Func<Task> act = () => _sut.Execute(code);

        var assertion = await act.Should()
            .ThrowAsync<WorkingFamiliesCorrectionDataException>();

        assertion.Which.Should().BeSameAs(failure);
    }

    private static WorkingFamiliesCorrectionRecord CreateRecord(string code)
    {
        return new WorkingFamiliesCorrectionRecord
        {
            EligibilityCode = code,
            SummaryId = "summary-id",
            LatestEventId = "event-id",
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
            },
            ValidityStartDate = new DateTime(2025, 1, 1),
            ValidityEndDate = new DateTime(2025, 3, 31),
            DiscretionaryValidityStartDate = new DateTime(2025, 1, 1),
            GracePeriodEndDate = new DateTime(2025, 8, 31),
            LastUpdatedDate = new DateTime(2025, 2, 2)
        };
    }
}