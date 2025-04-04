// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class AuditData
{
    public AuditType Type { get; set; }
    public string typeId { get; set; } = string.Empty;
    public string url { get; set; } = string.Empty;
    public string method { get; set; } = string.Empty;
    public string source { get; set; } = string.Empty;
    public string authentication { get; set; } = string.Empty;
    public string scope { get; set; } = string.Empty;
}