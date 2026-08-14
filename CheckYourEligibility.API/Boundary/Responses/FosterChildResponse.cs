public class FosterChildResponse
{
    // Eligibility Code Details

    public string EligibilityCode { get; set; } = string.Empty;

    public string ReconfirmationStatus { get; set; } = string.Empty;

    public string CodeStatus { get; set; } = string.Empty;

    public string EligibilityConfirmedOn { get; set; }

    public string ReconfirmFrom { get; set; }

    public string ReconfirmTo { get; set; }

    public string GracePeriodEnds { get; set; }


    // Child

    public Guid FosterChildId { get; set; }

    public string ChildFullName { get; set; } 

    public string ChildDateOfBirth { get; set; }

    public string PostCode { get; set; } 


    // Foster Family

    public Guid? FosterCarerId { get; set; }

    public string? CarerName { get; set; } 

    public string? PartnerName { get; set; }
}