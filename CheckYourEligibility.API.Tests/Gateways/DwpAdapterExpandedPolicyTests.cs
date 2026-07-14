using AutoFixture;
using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckYourEligibility.API.Tests.Gateways;

[TestFixture]
public class DwpAdapterExpandedPolicyTests
{
    private DwpAdapter _sut;
    private IFixture _fixture;
    private IConfiguration _configuration;
    private HttpClient _httpClient;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _httpClient = new HttpClient();
        var configForSmsApi = new Dictionary<string, string>();
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configForSmsApi).Build();
        _sut = new DwpAdapter(new NullLoggerFactory(), _httpClient, _configuration);
    }

    private EligibilityPolicy ExpandedPolicy => _fixture.Build<EligibilityPolicy>()
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.expanded)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();

    [Test]
    public void GuaranteedPensionCredit_Returns_Eligible_Tier_Targeted()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        request.data[0].attributes.endDate = null;
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.targeted));
    }

    [Test]
    public void JSA_Returns_Eligible_Tier_Targeted()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.job_seekers_allowance_income_based.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        request.data[0].attributes.endDate = null;
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.targeted));
    }

    [Test]
    public void ESA_Returns_Eligible_Tier_Targeted()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.employment_support_allowance_income_based.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        request.data[0].attributes.endDate = null;
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.targeted));
    }

    [Test]
    public void IncomeSupport_Returns_Eligible_Tier_Targeted()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.income_support.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        request.data[0].attributes.endDate = null;
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.targeted));
    }

    [Test]
    public void UC_LiveAwards_WithinThreshold_Returns_Eligible_Tier_Targeted()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.awards = new List<Award>
        {
            new Award { startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), endDate = DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd"), status = DwpAdapter.awardStatusLive, assessmentAttributes = new AssessmentAttributes { takeHomePay = 1000 } }
        };
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.targeted));
    }

    [Test]
    public void UC_LiveAwards_AboveThreshold_Returns_Eligible_Tier_Expanded()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.awards = new List<Award>
        {
            new Award { startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), endDate = DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd"), status = DwpAdapter.awardStatusLive, assessmentAttributes = new AssessmentAttributes { takeHomePay = 70000 } }
        };
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((true, EligibilityTier.expanded));
    }

    [Test]
    public void NotEligible_Returns_Eligible_Tier_Null()
    {
        var request = _fixture.Create<DwpClaimsResponse>();
        // No matching benefit types, no awards
        foreach (var d in request.data)
        {
            d.attributes.benefitType = "other";
            d.attributes.awards = new List<Award>();
        }
        var result = _sut.CheckBenefitEntitlement(Guid.NewGuid().ToString(), request, CheckEligibilityType.FreeSchoolMeals, ExpandedPolicy);
        result.Should().Be((false, null));
    }
}
