using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.Core.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.Core.IntegrationTests.Concurrency;

[TestFixture]
[NonParallelizable]
public sealed class EligibilityCodeConcurrencyTests
{
    private const long RangeStart = 40000000001L;
    private const long RangeEnd = 49999999999L;

    [SetUp]
    public async Task ResetEligibilityCodeRange()
    {
        await using var context = SqlServerFixture.CreateContext();

        await context.EligibilityCodeRanges
            .Where(range => range.Name == EligibilityCodeType.Foster)
            .ExecuteUpdateAsync(update => update
                .SetProperty(
                    range => range.NextAvailableCode,
                    RangeStart));
    }

    [Test]
    public async Task ConcurrentAllocations_ReturnUniqueValidEligibilityCodes()
    {
        const int allocationCount = 10;

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var allocationTasks = Enumerable.Range(0, allocationCount)
            .Select(async _ =>
            {
                await startGate.Task;

                await using var context =
                    SqlServerFixture.CreateContext();

                var gateway = new FosterFamiliesGateway(
                    context,
                    NullLogger<FosterFamiliesGateway>.Instance);

                return await gateway.GetEligibilityCodeForFosterChild();
            })
            .ToArray();

        // Release every task together to create competing database writes.
        startGate.SetResult(true);

        var codes = await Task.WhenAll(allocationTasks);

        codes.Should().HaveCount(allocationCount);
        codes.Should().OnlyHaveUniqueItems();

        codes.Should().AllSatisfy(code =>
        {
            code.Should().HaveLength(11);
            code.Should().StartWith("4");

            long.Parse(code)
                .Should().BeInRange(RangeStart, RangeEnd);
        });
    }

    [Test]
    public async Task ConcurrentFosterFamilyCreations_CreateChildrenWithUniqueValidCodes()
    {
        const int creationCount = 10;

        const int localAuthorityId = 999999;

        await using (var setupContext = SqlServerFixture.CreateContext())
        {
            await setupContext.Database.ExecuteSqlRawAsync(
                """
        INSERT INTO [LocalAuthorities]
        (
            [LocalAuthorityID],
            [LaName],
            [Region],
            [IsDeleted],
            [SchoolCanReviewEvidence],
            [FreeSchoolMealsPolicyID],
            [EarlyYearsPupilPremiumPolicyID],
            [TwoYearPolicyID]
        )
        VALUES
        (
            999999,
            N'Integration Test Local Authority',
            N'Integration Test',
            0,
            0,
            1,
            2,
            3
        );
        """);
        }

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var creationTasks = Enumerable.Range(0, creationCount)
            .Select(async index =>
            {
                await startGate.Task;

                await using var context =
                    SqlServerFixture.CreateContext();

                var gateway = new FosterFamiliesGateway(
                    context,
                    NullLogger<FosterFamiliesGateway>.Instance);

                return await gateway.CreateFosterFamily(
                    BuildValidRequest(index, localAuthorityId));
            })
            .ToArray();

        // Release all ten complete creation requests together.
        startGate.SetResult(true);

        var results = await Task.WhenAll(creationTasks);

        results.Should().HaveCount(creationCount);

        await using var assertionContext =
            SqlServerFixture.CreateContext();

        var children = await assertionContext.FosterChildren
            .AsNoTracking()
            .ToListAsync();

        children.Should().HaveCount(creationCount);

        var codes = children
            .Select(child => child.EligibilityCode)
            .ToArray();

        codes.Should().OnlyContain(
            code => !string.IsNullOrWhiteSpace(code));

        codes.Should().OnlyHaveUniqueItems();

        codes.Should().AllSatisfy(code =>
        {
            code.Should().HaveLength(11);
            code.Should().StartWith("4");

            long.Parse(code!)
                .Should().BeInRange(RangeStart, RangeEnd);
        });

        var range = await assertionContext.EligibilityCodeRanges
            .SingleAsync(item => item.Name == EligibilityCodeType.Foster);

        range.NextAvailableCode
            .Should().Be(RangeStart + creationCount);
    }

    private static FosterFamilyRequest BuildValidRequest(
        int index,
        int localAuthorityId)
    {
        return new FosterFamilyRequest
        {
            HasPartner = true,
            SubmissionDate = DateTime.UtcNow,

            FosterCarer = new FosterCarerRequest
            {
                CarerFirstName = $"Carer{index}",
                CarerLastName = "Smith",
                CarerDateOfBirth = new DateTime(1980, 1, 1),
                CarerNationalInsuranceNumber =
                    $"AA{100000 + index:D6}A",
                LocalAuthorityID = localAuthorityId
            },

            Partner = new FosterPartnerRequest
            {
                PartnerFirstName = $"Partner{index}",
                PartnerLastName = "Smith",
                PartnerDateOfBirth = new DateTime(1980, 1, 1),
                PartnerNationalInsuranceNumber =
                    $"AB{200000 + index:D6}A"
            },

            FosterChild = new FosterChildRequest
            {
                ChildFirstName = $"Child{index}",
                ChildLastName = "Smith",
                ChildDateOfBirth = new DateTime(2022, 1, 1),
                ChildPostCode = "NNU 1AE"
            }
        };
    }
}