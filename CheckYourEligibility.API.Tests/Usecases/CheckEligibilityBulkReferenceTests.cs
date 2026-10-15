using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FeatureManagement.Domain.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using ValidationException = CheckYourEligibility.API.Domain.Exceptions.ValidationException;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CheckEligibilityBulkReferenceTests
{
    private Mock<ICheckEligibility> _checkGateway = null!;
    private Mock<IBulkCheck> _bulkCheckGateway = null!;
    private Mock<IServiceScopeFactory> _scopeFactory = null!;
    private CheckEligibilityBulkUseCase _sut = null!;

    private static readonly CheckEligibilityType[] EligibilityTypes =
    [
        CheckEligibilityType.FreeSchoolMeals,
        CheckEligibilityType.EarlyYearPupilPremium,
        CheckEligibilityType.TwoYearOffer,
        CheckEligibilityType.WorkingFamilies
    ];

    [SetUp]
    public void SetUp()
    {
        _checkGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
        _bulkCheckGateway = new Mock<IBulkCheck>(MockBehavior.Strict);
        _scopeFactory = new Mock<IServiceScopeFactory>();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(ICheckEligibility)))
            .Returns(_checkGateway.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        _scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        _sut = new CheckEligibilityBulkUseCase(
            new CheckEligibilityRequestDataValidator(),
            _checkGateway.Object,
            _bulkCheckGateway.Object,
            Mock.Of<IAudit>(),
            Mock.Of<ILogger<CheckEligibilityBulkUseCase>>(),
            _scopeFactory.Object);
    }

    private static IEnumerable<TestCaseData> ValidReferences()
    {
        var cases = new (string Name, string? Input, string? Expected)[]
        {
            ("Null", null, null),
            ("Empty", "", ""),
            ("WhitespaceOnly", new string(' ', 101), ""),
            ("PreservesContent", " \tMiXeD  ref, \"A\" / 001\r\n", "MiXeD  ref, \"A\" / 001"),
            ("99Characters", new string('r', 99), new string('r', 99)),
            ("100Characters", new string('r', 100), new string('r', 100)),
            ("Padded100Characters", " \t" + new string('r', 100) + "\r\n ", new string('r', 100))
        };

        foreach (var type in EligibilityTypes)
        foreach (var testCase in cases)
            yield return new TestCaseData(type, testCase.Input, testCase.Expected)
                .SetName($"Execute_Reference_{type}_{testCase.Name}");
    }

    [TestCaseSource(nameof(ValidReferences))]
    public async Task Execute_ValidReference_ForwardsTrimmedValue(
        CheckEligibilityType type, string? reference, string? expected)
    {
        var posted = CapturePostedRows();
        var request = CreateRequest(type, new[] { reference });

        var response = await _sut.Execute(request, type, 100, new CheckMetaData());
        var rows = await posted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        response.Data.Status.Should().Be(Messages.Processing);
        rows.Should().ContainSingle();
        rows[0].Reference.Should().Be(expected);
        rows[0].Type.Should().Be(type);
        _bulkCheckGateway.Verify(x => x.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Once);
    }

    [TestCaseSource(nameof(EligibilityTypes))]
    public async Task Execute_OmittedReference_AcceptsExistingApiRequests(CheckEligibilityType type)
    {
        var posted = CapturePostedRows();
        var request = CreateRequest(type, new string?[] { null }, includeReference: false);

        await _sut.Execute(request, type, 100, new CheckMetaData());
        var rows = await posted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        rows.Should().ContainSingle();
        rows[0].Reference.Should().BeNull();
    }

    [TestCaseSource(nameof(EligibilityTypes))]
    public async Task Execute_ReferenceOver100Characters_RejectsEntireBatch(CheckEligibilityType type)
    {
        var request = CreateRequest(type, new[] { "valid-reference", " \t" + new string('r', 101) + "\r\n " });

        Func<Task> act = () => _sut.Execute(request, type, 100, new CheckMetaData());

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.Errors.Should().ContainSingle();
        exception.Errors[0].Title.Should().Be("Reference must be 100 characters or fewer.");
        exception.Errors[0].Detail.Should().Be("row-2");
        exception.Errors[0].Status.Should().Be(400);
        _bulkCheckGateway.Verify(x => x.CreateBulkCheck(It.IsAny<BulkCheck>()), Times.Never);
        _scopeFactory.Verify(x => x.CreateScope(), Times.Never);
        _checkGateway.VerifyNoOtherCalls();
    }

    private TaskCompletionSource<CheckEligibilityRequestDataBase[]> CapturePostedRows()
    {
        _bulkCheckGateway.Setup(x => x.CreateBulkCheck(It.IsAny<BulkCheck>()))
            .ReturnsAsync("bulk-check-id");

        var posted = new TaskCompletionSource<CheckEligibilityRequestDataBase[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _checkGateway.Setup(x => x.PostCheck(
                It.IsAny<IEnumerable<IEligibilityServiceType>>(),
                It.IsAny<string>(),
                It.IsAny<CheckMetaData>()))
            .Callback<IEnumerable<IEligibilityServiceType>, string, CheckMetaData>(
                (rows, _, _) => posted.TrySetResult(rows.Cast<CheckEligibilityRequestDataBase>().ToArray()))
            .Returns(Task.CompletedTask);

        return posted;
    }

    private static CheckEligibilityRequestBulk CreateRequest(
        CheckEligibilityType type, IEnumerable<string?> references, bool includeReference = true)
    {
        var rows = references.Select((reference, index) =>
        {
            var row = new Dictionary<string, object?>
            {
                ["LastName"] = "Smith",
                ["DateOfBirth"] = type == CheckEligibilityType.WorkingFamilies ? "2023-01-01" : "1990-01-01",
                ["NationalInsuranceNumber"] = "AB123456C",
                ["ClientIdentifier"] = $"row-{index + 1}",
                ["Order"] = index + 1
            };
            if (type == CheckEligibilityType.WorkingFamilies)
                row["EligibilityCode"] = "50012345678";
            if (includeReference)
                row["Reference"] = reference;
            return row;
        });

        // Exercise JSON request binding as well as the real batch validator.
        var json = JsonConvert.SerializeObject(new { Data = rows });
        return type == CheckEligibilityType.WorkingFamilies
            ? JsonConvert.DeserializeObject<CheckEligibilityRequestWorkingFamiliesBulk>(json)!
            : JsonConvert.DeserializeObject<CheckEligibilityRequestBulk>(json)!;
    }
}
