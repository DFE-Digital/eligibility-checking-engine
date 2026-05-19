using CheckYourEligibility.API.Domain;

public class WorkingFamilyEventByEligibilityCodeRepsonse
{
    public List<WorkingFamilyEventByEligibilityCodeRepsonseItem> Data;
}

public class WorkingFamilyEventByEligibilityCodeRepsonseItem
{ 
    
    public WorkingFamilyEventType Event  { get; set; }
    public WorkingFamiliesEventEligibilityCodeRepsonseRecord Record { get; set; }
}

public class WorkingFamiliesEventEligibilityCodeRepsonseRecord
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

