// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.CsvImport;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface INotify
{
    void SendNotification(NotificationRequest data);
}