public class FosterFamilyCreatedResponse : EligibilityCodeResponse
{
    public Guid FosterCarerId { get; init; }
    public string ChildName { get; init; }
}