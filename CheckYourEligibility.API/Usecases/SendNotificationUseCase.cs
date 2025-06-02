using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

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

        await _auditGateway.CreateAuditEntry(AuditType.Notification, notificationRequest.Data.Email);
        return new NotificationResponse();
    }
}