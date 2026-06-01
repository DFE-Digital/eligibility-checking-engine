using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Polly;

namespace CheckYourEligibility.API.Usecases;

public interface ILocalAuthoritiesUseCase
{
    Task<LocalAuthoritySettingsResponse> Execute(int laCode);
}

public class LocalAuthoritiesUseCase : ILocalAuthoritiesUseCase
{

    private readonly ILocalAuthority _localAuthorityGateway;
    private readonly IEligibilityPolicy _eligibilityPolicy;



    public LocalAuthoritiesUseCase(ILocalAuthority localAuthorityGateway, IEligibilityPolicy eligibilityPolicy)
    {
        _localAuthorityGateway = localAuthorityGateway;
        _eligibilityPolicy = eligibilityPolicy;
    }

    public async Task<LocalAuthoritySettingsResponse> Execute(int laCode)
    {

        var la = await _localAuthorityGateway.GetLocalAuthorityById(laCode, null) ?? throw new NotFoundException();


        var policyMap = new Dictionary<CheckEligibilityType, int> {
                {CheckEligibilityType.FreeSchoolMeals, la.FreeSchoolMealsPolicyID },
                {CheckEligibilityType.EarlyYearPupilPremium, la.EarlyYearsPupilPremiumPolicyID},
                {CheckEligibilityType.TwoYearOffer, la.TwoYearPolicyID},

            };

        List<EligibilityPolicyResponse> eligiblityPolicies = new();
        foreach (var (checkType, policyId) in policyMap)
        {      
            eligiblityPolicies.Add(await BuildPolicyResponse(checkType, policyId));
        }

        return new LocalAuthoritySettingsResponse
        {
            SchoolCanReviewEvidence = la.SchoolCanReviewEvidence,
            EligibilityPolicies = eligiblityPolicies

        };

    }

    private async Task<EligibilityPolicyResponse> BuildPolicyResponse(CheckEligibilityType checkType, int policyId)
    {
        if (policyId == 0)
        {
            return new EligibilityPolicyResponse(
                checkType.ToString(),
                EligibilityCriteria.standard.ToString()
            );
        }

        var policyRecord = await _eligibilityPolicy.GeEligibilityPolicyByIdAsync(policyId);

        return new EligibilityPolicyResponse(
            policyRecord.CheckType.ToString(),
            policyRecord.EligibilityCriteria.ToString()
        );
    }


}


