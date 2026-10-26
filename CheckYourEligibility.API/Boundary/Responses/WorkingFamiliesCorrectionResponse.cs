namespace CheckYourEligibility.API.Boundary.Responses;

public class WorkingFamiliesCorrectionResponse
{
    public required WorkingFamiliesCorrectionRecord Data { get; set; }
}

public class WorkingFamiliesCorrectionRecord
{
    public required string EligibilityCode { get; set; }
    public required string SummaryId { get; set; }
    public required string LatestEventId { get; set; }

    public required WorkingFamiliesCorrectionChild Child { get; set; }
    public required WorkingFamiliesCorrectionAdult Carer { get; set; }
    public WorkingFamiliesCorrectionAdult? Partner { get; set; }

    public bool HasPartner => Partner != null;

    public DateTime ValidityStartDate { get; set; }
    public DateTime ValidityEndDate { get; set; }
    public DateTime DiscretionaryValidityStartDate { get; set; }
    public DateTime? GracePeriodEndDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
}

public class WorkingFamiliesCorrectionChild
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Postcode { get; set; }
}

public class WorkingFamiliesCorrectionAdult
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? NationalInsuranceNumber { get; set; }
}