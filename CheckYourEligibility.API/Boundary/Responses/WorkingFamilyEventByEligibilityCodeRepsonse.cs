using CheckYourEligibility.API.Domain;

public class WorkingFamilyEventByEligibilityCodeRepsonse
{
    public List<WorkingFamilyEventByEligibilityCodeRepsonseItem> Data;
}

public class WorkingFamilyEventByEligibilityCodeRepsonseItem : WorkingFamiliesEvent
{ 
    
    public WorkingFamilyEventType Event  { get; set; }

}

public enum WorkingFamilyEventType
{
    Application,
    Reconfirm
}

