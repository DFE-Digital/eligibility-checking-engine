namespace CheckYourEligibility.API.Boundary.Responses;

public class LocalAuthoritySettingsResponse
{
    public bool SchoolCanReviewEvidence { get; set; }
    public IList<EligibilityPolicyResponse> EligibilityPolicies { get; set; }
}
public partial class EligibilityPolicyResponse { 


    public string CheckType { get; set; }
    public string EligibilityCriteria { get; set; }


    public EligibilityPolicyResponse(string checkType, string eligibilityCriteria)
    {
        CheckType = checkType;
        EligibilityCriteria = eligibilityCriteria;
    }


}