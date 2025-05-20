// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface INotify
{
    void SendNotification(NotificationRequest data);
}