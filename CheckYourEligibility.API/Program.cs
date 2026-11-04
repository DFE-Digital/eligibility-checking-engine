using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using CheckYourEligibility.API;
using CheckYourEligibility.API.Telemetry;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Middleware;
using CheckYourEligibility.Core.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// 1. Configure Services
// ------------------------

// Application Insights Telemetry
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Disable adaptive sampling to capture all telemetry
    options.EnableAdaptiveSampling = false;
});

// Register IHttpContextAccessor to access HttpContext in Telemetry Initializer
builder.Services.AddHttpContextAccessor();

// Register the TelemetryInitializer to attach user information to telemetry data
builder.Services.AddSingleton<ITelemetryInitializer, UserTelemetryInitializer>();

// Add Controllers with JSON options
builder.Services.AddControllers()
    .AddNewtonsoftJson()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Configure Azure Key Vault if environment variable is set
if (Environment.GetEnvironmentVariable("ECE_KEY_VAULT_NAME") != null)
{
    var keyVaultName = Environment.GetEnvironmentVariable("ECE_KEY_VAULT_NAME");
    var kvUri = $"https://{keyVaultName}.vault.azure.net";

    builder.Configuration.AddAzureKeyVault(
        new Uri(kvUri),
        new DefaultAzureCredential(),
        new AzureKeyVaultConfigurationOptions
        {
            ReloadInterval = TimeSpan.FromSeconds(60 * 10)
        }
    );
}

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1-admin",
        new OpenApiInfo
        {
            Title = "ECE API - V1",
            Version = "v1.4-admin",
            Description =
                "DFE Eligibility Checking Engine: API to perform Checks determining eligibility for entitlements via integration with OGDs"
        }
    );
    c.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "ECE Local Authority API - V1",
            Version = "v1.4",
            Description =
                "DFE Eligibility Checking Engine: API to perform Checks determining eligibility for entitlements via integration with OGDs" +
                (!string.IsNullOrEmpty(builder.Configuration.GetValue<string>("TestData:SampleData"))
                    ? "<br /><br />Test data can be downloaded <a href='" +
                      builder.Configuration.GetValue<string>("TestData:SampleData") + "'>here</a>."
                    : ""
                )
        });

    c.AddSecurityDefinition(
        "oauth2",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri(builder.Configuration.GetValue<string>("Host") + "/oauth2/token"),
                    Scopes = builder.Configuration.GetSection("Jwt").GetSection("Scopes").Get<List<string>>()
                        .ToDictionary(x => x, x => x)
                }
            }
        });

    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = "oauth2",
                        Type = ReferenceType.SecurityScheme
                    }
                },
                new List<string>()
            }
        });

    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (!apiDesc.TryGetMethodInfo(out var methodInfo)) return false;

        if (docName == "v1-admin") return true;
        if (apiDesc.RelativePath.StartsWith("check/")) return true;
        if (apiDesc.RelativePath.StartsWith("bulk-check/")) return true;
        if (apiDesc.RelativePath.StartsWith("oauth2/")) return true;

        return false;
    });

    var filePath = Path.Combine(AppContext.BaseDirectory, "CheckYourEligibility.API.xml");
    c.IncludeXmlComments(filePath);
    c.ExampleFilters();
    c.DocumentFilter<CheckYourEligibility.API.Filters.RemoveTypeFromRequestExamplesFilter>();
});

builder.Services.AddSwaggerExamplesFromAssemblyOf<IEligibilityServiceType>();
// Register Database and other services
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddAzureClients(builder.Configuration);
builder.Services.AddServices();
builder.Services.AddExternalServices(builder.Configuration);
builder.Services.AddJwtSettings(builder.Configuration);
builder.Services.AddTestFactories(builder.Configuration);

// Configure IIS and Kestrel server options
builder.Services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = int.MaxValue; });
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // Default is 30 MB
});

// Add Authorization
builder.Services.AddAuthorization(builder.Configuration);

// Certificate forwarding for Azure App Service (reads X-ARR-ClientCert header)
builder.Services.AddCertificateForwarding(options =>
{
    options.CertificateHeader = "X-ARR-ClientCert";
    options.HeaderConverter = headerValue =>
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return null!;
        try
        {
            var bytes = Convert.FromBase64String(headerValue);
            return new System.Security.Cryptography.X509Certificates.X509Certificate2(bytes);
        }
        catch (FormatException)
        {
            return null!;
        }
    };
});

builder.Services.AddSwaggerGen(c => { c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First()); });

var app = builder.Build();

app.MapHealthChecks("/healthcheck");

// ------------------------
// 2. Configure Middleware Pipeline
// ------------------------

// 2.1. Exception Handling
if (app.Environment.IsDevelopment())
{
    // DeveloperExceptionPage provides detailed exception information in Development
    app.UseDeveloperExceptionPage();
}
else
{
    // ExceptionHandler handles exceptions in Production and redirects to a generic error page
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// 2.2. HTTPS Redirection
app.UseHttpsRedirection();

// 2.2.1. Certificate Forwarding (Azure App Service sends client certs via X-ARR-ClientCert header)
app.UseCertificateForwarding();

// 2.3. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 2.5. Swagger Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "ECE Local Authority API - V1");
    c.SwaggerEndpoint("v1-admin/swagger.json", "ECE API - V1");
});

// 2.6. Map Controllers
app.MapControllers();

// 2.7 RateLimiter Middleware
app.UseWhen(context => context.Request.Method == RequestMethod.Post.ToString() &&
                       (context.Request.Path.StartsWithSegments("/check") || context.Request.Path.StartsWithSegments("/bulk-check")),
    app => app.UseCustomRateLimiter(
            new RateLimiterMiddlewareOptions
            {
                PartionName = "Authority-Id-Minute",
                WindowLength = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimit:Policies:Authority-Id-Minute:WindowLength", 1)),
                PermitLimit = builder.Configuration.GetValue("RateLimit:Policies:Authority-Id-Minute:PermitLimit", 250)
            })
        .UseCustomRateLimiter(
            new RateLimiterMiddlewareOptions
            {
                PartionName = "Authority-Id-Hour",
                WindowLength = TimeSpan.FromHours(builder.Configuration.GetValue("RateLimit:Policies:Authority-Id-Hour:WindowLength", 1)),
                PermitLimit = builder.Configuration.GetValue("RateLimit:Policies:Authority-Id-Hour:PermitLimit", 5000)
            }));

app.Run();

[ExcludeFromCodeCoverage]
public partial class Program
{
}