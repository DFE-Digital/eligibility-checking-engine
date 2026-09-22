namespace CheckYourEligibility.Core.Boundary.Responses;

public class FosterChildResponse
{
    // Eligibility Code Details

    public string EligibilityCode { get; set; } = string.Empty;

    public string ReconfirmationStatus { get; set; }

    public string CodeStatus { get; set; } = string.Empty;

    public DateTime EligibilityConfirmedOn { get; set; }

    public DateTime ValidityStartDate { get; set; }

    public DateTime ValidityEndDate { get; set; }

    public string ReconfirmBetween { get; set; }

    public DateTime GracePeriodEnds { get; set; }


    // Child

    public Guid FosterChildId { get; set; }

    public string ChildFullName { get; set; } 

    public DateTime ChildDateOfBirth { get; set; }

    public string PostCode { get; set; } 


    // Foster Family

    public Guid? FosterCarerId { get; set; }

    public string? CarerName { get; set; } 

    public string? PartnerName { get; set; }
}