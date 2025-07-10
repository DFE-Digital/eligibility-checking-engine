// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Domain.Enums;

public enum CheckEligibilityStatus
{
    queuedForProcessing,
    parentNotFound,
    eligible,
    notEligible,
    error,
    notFound
}