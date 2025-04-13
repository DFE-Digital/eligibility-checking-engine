// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class NotificationRequest
{
    public NotificationRequestData Data { get; set; }
}

public class NotificationRequestData
{
    public string Email { get; set; }
    public NotificationType Type { get; set; }
    public Dictionary<string, object>? Personalisation { get; set; }
}