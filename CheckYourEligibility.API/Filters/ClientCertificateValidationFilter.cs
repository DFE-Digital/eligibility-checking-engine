using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CheckYourEligibility.API.Filters;

/// <summary>
/// Action filter that validates the inbound client certificate on requests to the eligibility-events endpoint.
/// Checks are independently configurable: CN, Issuer, SerialNumber, Thumbprint.
/// Disable all checks by setting Ecs:EligibilityEvents:InboundCertificate:Enabled to false.
/// </summary>
public class ClientCertificateValidationFilter : IAsyncActionFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientCertificateValidationFilter> _logger;

    public ClientCertificateValidationFilter(
        IConfiguration configuration,
        ILogger<ClientCertificateValidationFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var enabled = _configuration.GetValue<bool>("Ecs:EligibilityEvents:InboundCertificate:Enabled");
        if (!enabled)
        {
            _logger.LogDebug("Inbound client certificate validation is disabled");
            await next();
            return;
        }

        var certificate = await context.HttpContext.Connection.GetClientCertificateAsync();

        if (certificate == null)
        {
            _logger.LogWarning("Inbound client certificate validation failed — no certificate presented");
            context.Result = new ObjectResult(new { error = "Client certificate required" })
            {
                StatusCode = 403
            };
            return;
        }

        _logger.LogInformation(
            "Inbound client certificate received — Subject: {Subject}, Issuer: {Issuer}, Thumbprint: {Thumbprint}, SerialNumber: {SerialNumber}",
            certificate.Subject, certificate.Issuer, certificate.Thumbprint, certificate.SerialNumber);

        var failures = new List<string>();

        var expectedCN = _configuration["Ecs:EligibilityEvents:InboundCertificate:ExpectedCN"];
        if (!string.IsNullOrEmpty(expectedCN))
        {
            var cn = certificate.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false);
            if (!string.Equals(cn, expectedCN, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"CN mismatch: expected '{expectedCN}', got '{cn}'");
            }
        }

        var expectedIssuer = _configuration["Ecs:EligibilityEvents:InboundCertificate:ExpectedIssuer"];
        if (!string.IsNullOrEmpty(expectedIssuer))
        {
            if (!string.Equals(certificate.Issuer, expectedIssuer, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Issuer mismatch: expected '{expectedIssuer}', got '{certificate.Issuer}'");
            }
        }

        var expectedSerial = _configuration["Ecs:EligibilityEvents:InboundCertificate:ExpectedSerialNumber"];
        if (!string.IsNullOrEmpty(expectedSerial))
        {
            if (!string.Equals(certificate.SerialNumber, expectedSerial, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"SerialNumber mismatch: expected '{expectedSerial}', got '{certificate.SerialNumber}'");
            }
        }

        var expectedThumbprint = _configuration["Ecs:EligibilityEvents:InboundCertificate:ExpectedThumbprint"];
        if (!string.IsNullOrEmpty(expectedThumbprint))
        {
            if (!string.Equals(certificate.Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Thumbprint mismatch: expected '{expectedThumbprint}', got '{certificate.Thumbprint}'");
            }
        }

        if (failures.Count > 0)
        {
            _logger.LogWarning(
                "Inbound client certificate validation failed — {Failures}",
                string.Join("; ", failures));

            context.Result = new ObjectResult(new { error = "Client certificate validation failed" })
            {
                StatusCode = 403
            };
            return;
        }

        _logger.LogInformation("Inbound client certificate validation passed");
        await next();
    }
}
