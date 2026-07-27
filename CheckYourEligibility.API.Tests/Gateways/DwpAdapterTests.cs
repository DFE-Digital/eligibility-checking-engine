// Ignore Spelling: Levenshtein

using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Adapters;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;

namespace CheckYourEligibility.API.Tests;

public class DwpAdapterTests : TestBase.TestBase
{
    // private IEligibilityCheckContext _fakeInMemoryDb;
    private IConfiguration _configuration;
    private DwpAdapter _sut;
    private HttpClient httpClient;

    [SetUp]
    public void Setup()
    {
        httpClient = new HttpClient();        

        //"c": "ecs.education.gov.uk",
        //"EcsServiceVersion": "20170701",
        //"EcsLAId": "999",
        //"EcsSystemId": "ECE43342",
        //"EcsPassword": "jiK65zxTmJ",


        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var configForSmsApi = new Dictionary<string, string>
        {
            { "Dwp:EcsHost", "ecs.education.gov.uk" },
            { "Dwp:EcsServiceVersion", "20170701" },
            { "Dwp:EcsLAId", "999" },
            { "Dwp:EcsSystemId", "testId" },
            { "Dwp:EcsPassword", "testpassword" },
            { "Dwp:UseEcsForChecks", "true" },
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";


        _sut = new DwpAdapter(new NullLoggerFactory(), httpClient, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public void Given_ResponseBody_With_DwpErrors_ProcessCapiResponseCode_Should_Return_Parsed_Code()
    {
        // Arrange
        var responseBody = "{\"errors\":[{\"code\":\"12345\",\"detail\":\"example\"}]}";

        // Act
        var result = CAPIClaimResponseBase.ProcessCapiResponseCode(responseBody);

        // Assert
        result.Should().Be(12345);
    }

    [Test]
    public void Given_ResponseBody_Without_DwpErrors_ProcessCapiResponseCode_Should_Return_Zero()
    {
        // Arrange
        var responseBody = "{\"data\":[]}";

        // Act
        var result = CAPIClaimResponseBase.ProcessCapiResponseCode(responseBody);

        // Assert
        result.Should().Be(0);
    }

    [Test]
    public void Given_Claims_have_pensions_credit_and_Policy_Is_Standard_CheckBenefitEntitlement_Should_Return_true_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        request.data[0].attributes.endDate = null;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true); //isEntitled
        response.Item2.Should().BeNull(); //tier
    }

    [Test]
    public void Given_Claims_have_pensions_credit_and_Policy_IsStandard_CheckBenefitEntitlement_Should_Return_false_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.status = "not entitled";
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false); //IsEntitled
        response.Item2.Should().BeNull(); // Tier
    }

    [Test]
    public void Given_Claims_have_job_seekers_allowance_income_based_CheckBenefitEntitlement_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.job_seekers_allowance_income_based.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true); //IsEntitled
        response.Item2.Should().BeNull(); //Tier
    }

    [Test]
    public void Given_Claims_have_income_and_Policy_IsStandard_support_CheckBenefitEntitlement_Should_Return_true_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.benefitType = DwpBenefitType.income_support.ToString();
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    [Test]
    public void Given_Claims_have_employment_support_allowance_income_based_and_Policy_IsStandard_CheckBenefitEntitlement_Should_Return_true_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();

        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.employment_support_allowance_income_based.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     UC1 (61667 pence) One instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_1_and_Policy_IsStandard_Should_Return_false_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();

        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100000 }
            }
        };


        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     UC1 616.66 One instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_and_Policy_IsStandard_CheckBenefitEntitlement_1_Should_Return_true_Tier_Should_Ne_Null()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
        .With(x => x.ID, 1)
        .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
        .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
        .With(x => x.UniversalCreditThreshold, 61667)
        .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }


    /// <summary>
    ///     UC2 1233.33 two instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_2_Should_Return_false()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500000 } //pence
            },
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500000 } //pence
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     UC1 1233.33 two instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_2_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            },
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     UC2 184999 pence two instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_3_Should_Return_false()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500000 }
            },
            new()
            {
                endDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500000 }
            },
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500000 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     UC1 1849.99 two instance of an award with status live within threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_universal_credit_CheckBenefitEntitlement_3_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        request.data[0].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[0].attributes.status = DwpAdapter.statusInPayment;
        request.data[0].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            },
            new()
            {
                endDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-2).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            },
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-d"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     Ended pension credit
    ///     UC1 616.66 One instance of an award with status live below threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_Ended_pensions_credit_and_universal_credit_CheckBenefitEntitlement_1_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        //PC
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.endDate = DateTime.Now.ToString("yyyy-MM-dd");
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 500 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     Ended pension credit
    ///     UC1 (61667 pence) One instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_have_Ended_pensions_credit_and_universal_credit_CheckBenefitEntitlement_1_Should_Return_false()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();

        //PC
        request.data[0].attributes.benefitType = DwpBenefitType.pensions_credit.ToString();
        request.data[0].attributes.endDate = DateTime.Now.ToString("yyyy-MM-dd");
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100000 } //in pence
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     JSA not ended
    ///     UC1 616.66 One instance of an award with status superseded above threshold
    /// </summary>
    [Test]
    public void Given_Claims_no_live_universal_credit_JSA_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        //JSA
        request.data[0].attributes.benefitType = DwpBenefitType.job_seekers_allowance_income_based.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = "superseded",
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 1000 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     ESA not ended
    ///     UC1 616.66 One instance of an award with status superseded above threshold
    /// </summary>
    [Test]
    public void Given_Claims_no_live_universal_credit_ESA_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        //ESA
        request.data[0].attributes.benefitType = DwpBenefitType.employment_support_allowance_income_based.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = "superseded",
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 1000 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     Income Support not ended
    ///     UC1 616.66 One instance of an award with status superseded above threshold
    /// </summary>
    [Test]
    public void Given_Claims_no_live_universal_credit_Income_Support_Should_Return_true()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        //IS
        request.data[0].attributes.benefitType = DwpBenefitType.income_support.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = "superseded",
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 1000 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(true);
        response.Item2.Should().BeNull();
    }

    /// <summary>
    ///     Income Support not ended
    ///     UC1 61667 pence One instance of an award with status live above threshold
    /// </summary>
    [Test]
    public void Given_Claims_universal_credit_above_threshold_JSA_Should_Return_false()
    {
        // Arrange
        var eligibilityPolicy = _fixture.Build<EligibilityPolicy>()
            .With(x => x.ID, 1)
            .With(x => x.CheckType, CheckEligibilityType.FreeSchoolMeals)
            .With(x => x.EligibilityCriteria, EligibilityCriteria.standard)
            .With(x => x.UniversalCreditThreshold, 61667)
            .Create();
        var citizenGuid = Guid.NewGuid().ToString();
        var request = _fixture.Create<DwpClaimsResponse>();
        //JSA
        request.data[0].attributes.benefitType = DwpBenefitType.job_seekers_allowance_income_based.ToString();
        request.data[0].attributes.endDate = null;
        request.data[0].attributes.status = DwpAdapter.decision_entitled;
        //UC
        request.data[1].attributes.benefitType = DwpBenefitType.universal_credit.ToString();
        request.data[1].attributes.status = DwpAdapter.statusInPayment;
        request.data[1].attributes.awards = new List<Award>
        {
            new()
            {
                endDate = DateTime.Now.ToString("yyyy-MM-dd"), startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"),
                status = DwpAdapter.awardStatusLive,
                assessmentAttributes = new AssessmentAttributes { takeHomePay = 100000 }
            }
        };

        // Act
        var response = _sut.CheckBenefitEntitlement(citizenGuid, request, CheckEligibilityType.FreeSchoolMeals, eligibilityPolicy);

        // Assert
        response.Item1.Should().Be(false);
        response.Item2.Should().BeNull();
    }

    private CheckProcessData GetCheckProcessData(CheckEligibilityRequestData request)
    {
        return new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth ?? "1990-01-01",
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = request.Type
        };
    }
}