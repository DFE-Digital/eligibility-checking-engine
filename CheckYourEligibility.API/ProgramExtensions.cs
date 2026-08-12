using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CheckYourEligibility.API.Filters;
using CheckYourEligibility.Core.Extensions;
namespace CheckYourEligibility.API;

[ExcludeFromCodeCoverage(Justification = "extension of program")]
public static class ProgramExtensions
{
    
    private static bool ByPassCertErrorsForTestPurposesDoNotDoThisInTheWild(
        HttpRequestMessage httpRequestMsg,
        X509Certificate2 certificate,
        X509Chain x509Chain,
        SslPolicyErrors policyErrors)
    {
        return true;
    }
    public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
    {


        // Register HttpClient normally
        services.AddHttpClient("Dwp", client =>
        {
            client.BaseAddress = new Uri(configuration["Dwp:BaseUrl"]);
        }).ConfigurePrimaryHttpMessageHandler(() => {

            var privateKeyBytes = Convert.FromBase64String(configuration["Dwp:ApiCertificate"]);
            var cert = new X509Certificate2(privateKeyBytes, (string)null, X509KeyStorageFlags.MachineKeySet);
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(cert);
            handler.ServerCertificateCustomValidationCallback = ByPassCertErrorsForTestPurposesDoNotDoThisInTheWild;
            return handler;
        })
    .AddPolicyHandler((sp, msg) =>  
    {
       var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PollyRetry");
        return HttpClientPolicies.GetRetryPolicyWithJitter(logger, "DWP");
    }).AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());

        // Register DwpAdapter as singleton, inject IHttpClientFactory
        services.AddSingleton<IDwpAdapter>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Dwp");
            var config = sp.GetRequiredService<IConfiguration>();
            return new DwpAdapter(loggerFactory, httpClient, config);
        });

        // Register ECS Eligibility Events adapter for forwarding to ECS via Barracuda
        var ecsBaseUrl = configuration["Ecs:EligibilityEvents:BaseUrl"];
        if (!string.IsNullOrEmpty(ecsBaseUrl))
        {
            services.AddHttpClient("EcsEligibilityEvents", client =>
            {
                client.BaseAddress = new Uri(ecsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
               
            }).AddPolicyHandler((sp, msg) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PollyRetry");
                return HttpClientPolicies.GetRetryPolicyWithJitter(logger, "ECS");
            })

            .AddPolicyHandler(HttpClientPolicies.GetCircuitBreakerPolicy());
            
            services.AddSingleton<IEcsEligibilityEventsAdapter>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<EcsEligibilityEventsAdapter>>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("EcsEligibilityEvents");
                var config = sp.GetRequiredService<IConfiguration>();
                return new EcsEligibilityEventsAdapter(logger, httpClient, config);
            });
        }

        // Register client certificate validation filter
        services.AddScoped<ClientCertificateValidationFilter>();

        return services;
    }

    public static IServiceCollection AddJwtSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var jwtSettings = new JwtSettings();
            config.GetSection("Jwt").Bind(jwtSettings);
            return jwtSettings;
        });

        return services;
    }

    public static IServiceCollection AddAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Issuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
                };

                options.Events = new JwtBearerEvents
                {
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.RequireLocalAuthorityScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasSingleScope(configuration["Jwt:Scopes:local_authority"] ?? "local_authority")));
            
            options.AddPolicy(PolicyNames.RequireMultiAcademyTrustScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScopeWithColon(configuration["Jwt:Scopes:multi_academy_trust"] ?? "multi_academy_trust")));

            options.AddPolicy(PolicyNames.RequireLaOrMatScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasSingleScope(configuration["Jwt:Scopes:local_authority"] ?? "local_authority") ||
                    context.User.HasScopeWithColon(configuration["Jwt:Scopes:multi_academy_trust"] ?? "multi_academy_trust")));
            options.AddPolicy(PolicyNames.RequireLaOrMatOrSchoolScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasSingleScope(configuration["Jwt:Scopes:local_authority"] ?? "local_authority") ||
                    context.User.HasScopeWithColon(configuration["Jwt:Scopes:multi_academy_trust"] ?? "multi_academy_trust") ||
                    context.User.HasScopeWithColon(configuration["Jwt:Scopes:establishment"] ?? "establishment")));
            options.AddPolicy(PolicyNames.RequireCheckScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:check"] ?? "check")));

            options.AddPolicy(PolicyNames.RequireApplicationScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:application"] ?? "application")));

            options.AddPolicy(PolicyNames.RequireAdminScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:admin"] ?? "admin")));

            /* new policies for "bulk_check","establishment","user" and "engine" */
            options.AddPolicy(PolicyNames.RequireBulkCheckScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:bulk_check"] ?? "bulk_check")));

            options.AddPolicy(PolicyNames.RequireEstablishmentScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:establishment"] ?? "establishment")));

            options.AddPolicy(PolicyNames.RequireUserScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:user"] ?? "user")));

            options.AddPolicy(PolicyNames.RequireEngineScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:engine"] ?? "engine")));

            options.AddPolicy(PolicyNames.RequireNotificationScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScope(configuration["Jwt:Scopes:notification"] ?? "notification")));

            options.AddPolicy(PolicyNames.RequireLaOrAdminScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasSingleScope(configuration["Jwt:Scopes:local_authority"] ?? "local_authority") ||
                    context.User.HasScope(configuration["Jwt:Scopes:admin"] ?? "admin")));
            options.AddPolicy(PolicyNames.RequireMatOrAdminScope, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasScopeWithColon(configuration["Jwt:Scopes:multi_academy_trust"] ?? "multi_academy_trust") ||
                    context.User.HasScope(configuration["Jwt:Scopes:admin"] ?? "admin")));            

            options.AddPolicy(PolicyNames.RequireFreeSchoolMealsAdminPortalSource, policy =>
                policy.RequireAssertion(context =>
                {
                    var checkSourceAndUserName = context.User.GetCheckSourceAndUserNameFromClientId();

                    return string.Equals(
                        checkSourceAndUserName.Item1,
                        "free-school-meals-admin",
                        StringComparison.OrdinalIgnoreCase);
                }));
        });
        return services;
    }
}