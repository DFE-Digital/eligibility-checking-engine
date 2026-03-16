using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CheckYourEligibility.API.Boundary.Requests;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Adapters;

public interface IEcsEligibilityEventsAdapter
{
    Task<HttpResponseMessage> ForwardPutAsync(string id, EligibilityEventRequest request);
    Task<HttpResponseMessage> ForwardDeleteAsync(string id);
}

[ExcludeFromCodeCoverage]
public class EcsEligibilityEventsAdapter : IEcsEligibilityEventsAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<EcsEligibilityEventsAdapter> _logger;

    public EcsEligibilityEventsAdapter(
        ILogger<EcsEligibilityEventsAdapter> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _baseUrl = configuration["Ecs:EligibilityEvents:BaseUrl"]
            ?? throw new InvalidOperationException("Ecs:EligibilityEvents:BaseUrl configuration is missing");

        var certBase64 = configuration["Ecs:EligibilityEvents:ClientCertificate"];
        if (!string.IsNullOrEmpty(certBase64))
        {
            var certBytes = Convert.FromBase64String(certBase64);
            var certificate = new X509Certificate2(certBytes, (string?)null, X509KeyStorageFlags.MachineKeySet);

            _logger.LogInformation(
                "ECS EligibilityEvents outbound certificate loaded — Thumbprint: {Thumbprint}, Subject: {Subject}, Expires: {NotAfter}",
                certificate.Thumbprint, certificate.Subject, certificate.NotAfter);

            if (certificate.NotAfter < DateTime.UtcNow.AddDays(30))
            {
                _logger.LogWarning(
                    "ECS EligibilityEvents outbound certificate expires within 30 days — NotAfter: {NotAfter}",
                    certificate.NotAfter);
            }

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = httpClient.BaseAddress,
                Timeout = httpClient.Timeout
            };
        }
    }

    public async Task<HttpResponseMessage> ForwardPutAsync(string id, EligibilityEventRequest request)
    {
        var safeId = id?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var url = $"{_baseUrl.TrimEnd('/')}/efe/api/v1/eligibility-events/{Uri.EscapeDataString(safeId!)}";

        _logger.LogInformation("Forwarding PUT eligibility-event to ECS — Id: {Id}", safeId);

        var stopwatch = Stopwatch.StartNew();

        var json = JsonConvert.SerializeObject(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(url, content);

        stopwatch.Stop();

        _logger.LogInformation(
            "ECS PUT eligibility-event response — Id: {Id}, StatusCode: {StatusCode}, ElapsedMs: {ElapsedMs}",
            safeId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        return response;
    }

    public async Task<HttpResponseMessage> ForwardDeleteAsync(string id)
    {
        var safeId = id?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var url = $"{_baseUrl.TrimEnd('/')}/efe/api/v1/eligibility-events/{Uri.EscapeDataString(safeId!)}";

        _logger.LogInformation("Forwarding DELETE eligibility-event to ECS — Id: {Id}", safeId);

        var stopwatch = Stopwatch.StartNew();

        var response = await _httpClient.DeleteAsync(url);

        stopwatch.Stop();

        _logger.LogInformation(
            "ECS DELETE eligibility-event response — Id: {Id}, StatusCode: {StatusCode}, ElapsedMs: {ElapsedMs}",
            safeId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
