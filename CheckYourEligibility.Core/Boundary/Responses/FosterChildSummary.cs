public class FosterChildSummaryResponse
{
    public Guid FosterChildId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public string EligibilityCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}