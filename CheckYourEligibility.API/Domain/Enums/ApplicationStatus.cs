// Ignore Spelling: Fsm

using System.ComponentModel;

namespace CheckYourEligibility.API.Domain.Enums;

public enum ApplicationStatus
{
    [Description("Entitled")] Entitled,
    [Description("Receiving Entitlement")] Receiving,
    [Description("Evidence Needed")] EvidenceNeeded,
    [Description("Sent for Review")] SentForReview,
    [Description("Reviewed Entitled")] ReviewedEntitled,
    [Description("Reviewed Not Entitled")] ReviewedNotEntitled,
    [Description("Archived")] Archived
}