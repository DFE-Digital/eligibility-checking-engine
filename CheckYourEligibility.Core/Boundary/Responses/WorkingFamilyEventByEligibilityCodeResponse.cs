using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class WorkingFamilyEventByEligibilityCodeResponse
{
    public List<WorkingFamilyEventByEligibilityCodeResponseItem> Data;
}

public class WorkingFamilyEventByEligibilityCodeResponseItem
{ 
    
    public WorkingFamilyEventType Event  { get; set; }
    public WorkingFamiliesEventEligibilityCodeResponseRecord Record { get; set; }
}

public class WorkingFamiliesEventEligibilityCodeResponseRecord
{
    public string EventId { get; set; }

    public DateTime? SubmissionDate { get; set; }
    public DateTime? DiscretionaryStartDate { get; set; }
    public DateTime? ValidityStartDate { get; set; }
    public DateTime? ValidityEndDate { get; set; }
    public DateTime? GracePeriodEndDate { get; set; }

    public string ParentNationalInsuranceNumber { get; set; }
    public string ParentFirstName { get; set; }
    public string ParentLastName { get; set; }
    public DateTime? ParentDateOfBirth { get; set; }

    public string PartnerNationalInsuranceNumber { get; set; }
    public string PartnerFirstName { get; set; }
    public string PartnerLastName { get; set; }
    public DateTime? PartnerDateOfBirth { get; set; }

    public string ChildFirstName { get; set; }
    public string ChildLastName { get; set; }
    public DateTime? ChildDateOfBirth { get; set; }
    public string ChildPostCode { get; set; }

}

public enum WorkingFamilyEventType
{
    Application,
    Reconfirm
}

