using CheckYourEligibility.Core.Boundary.Requests;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface INotify
{
    void SendNotification(NotificationRequest data);
}