using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways.Interfaces;
using Notify.Interfaces;
using Notify.Models.Responses;

namespace CheckYourEligibility.API.Gateways
{
    public class NotifyGateway : BaseGateway, INotify
    {
        private readonly INotificationClient _client;
        private readonly IConfiguration _configuration;

        public NotifyGateway(INotificationClient client, IConfiguration configuration)
        {
            _client = client;
            _configuration = configuration;
        }

        public void SendNotification(NotificationRequest notificationRequest)
        {
            string templateId = _configuration.GetValue<string>($"Notify:Templates:{notificationRequest.Data.Type.ToString()}");
            Deliver(templateId, notificationRequest.Data.Email, notificationRequest.Data.Personalisation);
        }

        private void Deliver(string templateId, string email, Dictionary<string, object> personalisation)
        {
            _client.SendEmail(email, templateId, personalisation, null, null);
        }
    }
}