namespace CheckYourEligibility.API.Boundary.Requests;

public class QueueMessageCheck
{
    public string Type { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;

    public string ProcessUrl { get; set; } = string.Empty;
    public string SetStatusUrl { get; set; } = string.Empty;
}