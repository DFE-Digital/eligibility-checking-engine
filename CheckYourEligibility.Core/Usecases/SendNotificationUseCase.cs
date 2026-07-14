using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases;

public interface ISendNotificationUseCase
{
    Task<NotificationResponse> Execute(NotificationRequest query);
}

public class SendNotificationUseCase : ISendNotificationUseCase
{
    private readonly IAudit _auditGateway;
    private readonly INotify _gateway;

    public SendNotificationUseCase(INotify gateway, IAudit auditGateway)
    {
        _gateway = gateway;
        _auditGateway = auditGateway;
    }

    public async Task<NotificationResponse> Execute(NotificationRequest notificationRequest)
    {
        _gateway.SendNotification(notificationRequest);

        return new NotificationResponse();
    }
}