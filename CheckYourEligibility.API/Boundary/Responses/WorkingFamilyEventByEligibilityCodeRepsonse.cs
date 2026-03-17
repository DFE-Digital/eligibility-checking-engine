using CheckYourEligibility.API.Domain;

public class WorkingFamilyEventByEligibilityCodeRepsonse
{
    public List<WorkingFamilyEventByEligibilityCodeRepsonseItem> Data;
}

public class WorkingFamilyEventByEligibilityCodeRepsonseItem
{ 
    
    public WorkingFamilyEventType Event  { get; set; }
    public WorkingFamiliesEvent Record { get; set; }
}

public enum WorkingFamilyEventType
{
    Application,
    Reconfirm
}

