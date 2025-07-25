using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses;

public class CheckEligibilityItemBase
{
    public string NationalInsuranceNumber { get; set; }

    public string Status { get; set; }

    public DateTime Created { get; set; }
}

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class CheckEligibilityItem : CheckEligibilityItemBase
{
    public string LastName { get; set; }

    public string DateOfBirth { get; set; }

    public string NationalAsylumSeekerServiceNumber { get; set; }

    public string? ClientIdentifier { get; set; }
    public string ValidityStartDate { get; set; }
    public string ValidityEndDate { get; set; }
    public string GracePeriodEndDate { get; set; }
    public string EligibilityCode { get; set; }
}

public class CheckEligibilityItemResponse
{
    public CheckEligibilityItem Data { get; set; }
    public CheckEligibilityResponseLinks Links { get; set; }
}