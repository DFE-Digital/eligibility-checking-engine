using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using CheckYourEligibility.Core.Database;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using CheckYourEligibility.Core.Data.Mappings;
using CheckYourEligibility.Core.UseCases;
using CheckYourEligibility.Core.UseCases.Internal;
using CheckYourEligibility.Core.Services;
using FeatureManagement.Domain.Validation;
using CheckYourEligibility.Core.Boundary.Requests;
using FluentValidation;
using CheckYourEligibility.Core.Gateways.Factories.Helper;
using CheckYourEligibility.Core.Gateways.Factories;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Extensions;

[ExcludeFromCodeCoverage(Justification = "extension of core services")]
public static class CoreExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("ConnectionString");

        services.AddDbContextFactory<EligibilityCheckContext>(options =>
          options.UseSqlServer(connectionString, x => x.MigrationsAssembly("CheckYourEligibility.Core")), lifetime: ServiceLifetime.Scoped);

        services.AddDbContext<IEligibilityCheckContext, EligibilityCheckContext>(options =>
            options.UseSqlServer(connectionString, x => x.MigrationsAssembly("CheckYourEligibility.Core"))
        );

        return services;
    }

    public static IServiceCollection AddAzureClients(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("Queue:ConnectionString");
        services.AddAzureClients(builder => { builder.AddQueueServiceClient(connectionString); });
        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Mapping
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddScoped<IEligibilityCheckDataResponseMapper, EligibilityCheckDataResponseMapper>();

        // Validators
        services.AddScoped<IValidator<IEligibilityServiceType>, CheckEligibilityRequestDataValidator>();

        // Factories
        services.AddSingleton<INotificationClientFactory, NotificationClientFactory>();

        // Gateways
        services.AddTransient<ICheckEligibility, CheckEligibilityGateway>();
        services.AddTransient<IEligibilityCheckReporting, EligibilityCheckReportingGateway>();
        services.AddTransient<IBulkCheck, BulkCheckGateway>();
        services.AddTransient<ICreateApplicationsFromBulkCheck, CreateApplicationsFromBulkCheckGateway>();
        services.AddTransient<ICheckingEngine, CheckingEngineGateway>();
        services.AddTransient<IStorageQueue, StorageQueueGateway>();
        services.AddTransient<IApplication, ApplicationGateway>();
        services.AddTransient<ILocalAuthority, LocalAuthorityGateway>();
        services.AddTransient<IAdministration, AdministrationGateway>();
        services.AddTransient<INotify, NotifyGateway>();
        services.AddTransient<IEcsAdapter, EcsAdapter>();
        services.AddTransient<IEstablishmentSearch, EstablishmentSearchGateway>();
        services.AddTransient<IUsers, UsersGateway>();
        services.AddTransient<IAudit, AuditGateway>();
        services.AddTransient<IHash, HashGateway>();
        services.AddTransient<IRateLimit, RateLimitGateway>();
        services.AddTransient<IWorkingFamiliesReporting, WorkingFamiliesReportingGateway>();
        services.AddTransient<IWorkingFamiliesDualRunningCheck, WorkingFamiliesDualRunningCheckGateway>();
        services.AddTransient<IWorkingFamiliesEvent, WorkingFamiliesEventGateway>();
        services.AddTransient<IMultiAcademyTrust, MultiAcademyTrustGateway>();
        services.AddTransient<IEligibilityPolicy, EligibilityPolicyGateway>();
        services.AddTransient<IFosterFamilies, FosterFamiliesGateway>();
        services.AddTransient<ITemporaryCodeUpload, TemporaryCodeUploadGateway>();
        services.AddTransient<IWorkingFamiliesCorrection, WorkingFamiliesCorrectionGateway>();

        // Use cases
        services.AddScoped<ICreateOrUpdateFSMParentUserUseCase, CreateOrUpdateFSMParentUserUseCase>();
        services.AddScoped<IGetUserRolesUseCase, GetUserRolesUseCase>();
        services.AddScoped<IAddUserRoleUseCase, AddUserRoleUseCase>();
        services.AddScoped<IRemoveUserRoleUseCase, RemoveUserRoleUseCase>();
        services.AddScoped<ISearchUsersUseCase, SearchUsersUseCase>();
        services.AddScoped<IGetUserUseCase, GetUserUseCase>();
        services.AddScoped<IUpdateUserEnabledUseCase, UpdateUserEnabledUseCase>();
        services.AddScoped<IAuthenticateUserUseCase, AuthenticateUserUseCase>();
        services.AddScoped<IGetCitizenClaimsUseCase, GetCitizenClaimsUseCase>();
        services.AddScoped<ISearchEstablishmentsUseCase, SearchEstablishmentsUseCase>();
        services.AddScoped<ICleanUpEligibilityChecksUseCase, CleanUpEligibilityChecksUseCase>();
        services.AddScoped<IImportEstablishmentsUseCase, ImportEstablishmentsUseCase>();
        services.AddScoped<IImportMatsUseCase, ImportMatsUseCase>();
        services.AddScoped<IImportFsmHomeOfficeDataUseCase, ImportFsmHomeOfficeDataUseCase>();
        services.AddScoped<IImportFsmHMRCDataUseCase, ImportFsmHMRCDataUseCase>();
        services.AddScoped<IImportWfHMRCDataUseCase, ImportWfHMRCDataUseCase>();
        services.AddScoped<IUpsertWorkingFamiliesEventUseCase, UpsertWorkingFamiliesEventUseCase>();
        services.AddScoped<IDeleteWorkingFamiliesEventUseCase, DeleteWorkingFamiliesEventUseCase>();
        services.AddScoped<IUpdateEstablishmentsPrivateBetaUseCase, UpdateEstablishmentsPrivateBetaUseCase>();
        services.AddScoped<ICreateApplicationUseCase, CreateApplicationUseCase>();
        services.AddScoped<ICreateApplicationsFromBulkCheckUseCase, CreateApplicationsFromBulkCheckUseCase>();
        services.AddScoped<IGetApplicationUseCase, GetApplicationUseCase>();
        services.AddScoped<ISearchApplicationsUseCase, SearchApplicationsUseCase>();
        services.AddScoped<IUpdateApplicationUseCase, UpdateApplicationUseCase>();
        services.AddScoped<IImportApplicationsUseCase, ImportApplicationsUseCase>();
        services.AddScoped<IDeleteApplicationUseCase, DeleteApplicationUseCase>();
        services.AddScoped<IRestoreArchivedApplicationStatusUseCase, RestoreArchivedApplicationStatusUseCase>();
        services.AddScoped<IProcessEligibilityBulkCheckUseCase, ProcessEligibilityBulkCheckUseCase>();
        services.AddScoped<ICheckEligibilityUseCase, CheckEligibilityUseCase>();
        services.AddScoped<ICheckEligibilityBulkUseCase, CheckEligibilityBulkUseCase>();
        services.AddScoped<IPreviewFosterFamilyCodeUseCase, PreviewFosterFamilyCodeUseCase>();
        services.AddScoped<IGetBulkCheckStatusesUseCase, GetBulkCheckStatusesUseCase>();
        services.AddScoped<IGetAllBulkChecksUseCase, GetAllBulkChecksUseCase>();
        services.AddScoped<IGetBulkCheckSummaryUseCase, GetBulkCheckSummaryUseCase>();
        services.AddScoped<IGetEligibilityCheckReportingUseCase, GetEligibilityCheckReportingUseCase>();
        services.AddScoped<IGetEligibilityReportStatusUseCase, GetEligibilityReportStatusUseCase>();
        services.AddScoped<IGetEligibilityCheckReportItemsUseCase, GetEligibilityCheckReportItemsUseCase>();
        services.AddScoped<IGetEligibilityReportHistoryUseCase, GetEligibilityReportHistoryUseCase>();
        services.AddScoped<IDeleteEligibilityCheckReportUseCase, DeleteEligibilityCheckReportUseCase>();
        services.AddScoped<IGetAllWorkingFamiliesEventsByEligibilityCodeUseCase, GetAllWorkingFamiliesEventsByEligibilityCodeUseCase>();
        services.AddScoped<IGetBulkUploadProgressUseCase, GetBulkUploadProgressUseCase>();
        services.AddScoped<ILocalAuthoritiesUseCase, LocalAuthoritiesUseCase>();
        services.AddScoped<IGetEstablishmentsByLocalAuthorityIdUseCase, GetEstablishmentsByLocalAuthorityIdUseCase>();
        services.AddScoped<IGetEstablishmentsByMultiAcademyTrustIdUseCase, GetEstablishmentsByMultiAcademyTrustIdIdUseCase>();
        services.AddScoped<IGetBulkUploadResultsUseCase, GetBulkUploadResultsUseCase>();
        services.AddScoped<IGetEligibilityCheckStatusUseCase, GetEligibilityCheckStatusUseCase>();
        services.AddScoped<IUpdateEligibilityCheckStatusUseCase, UpdateEligibilityCheckStatusUseCase>();
        services.AddScoped<IProcessEligibilityCheckUseCase, ProcessEligibilityCheckUseCase>();
        services.AddScoped<IGetEligibilityCheckItemUseCase, GetEligibilityCheckItemUseCase>();
        services.AddScoped<IDeleteBulkCheckUseCase, DeleteBulkCheckUseCase>();
        services.AddScoped<ISendNotificationUseCase, SendNotificationUseCase>();
        services.AddScoped<ICreateRateLimitEventUseCase, CreateRateLimitEventUseCase>();
        services.AddScoped<ICleanUpRateLimitEventsUseCase, CleanUpRateLimitEventsUseCase>();
        services.AddScoped<IGetFosterFamilyUseCase, GetFosterFamilyUseCase>();
        services.AddScoped<ICreateFosterFamilyUseCase, CreateFosterFamilyUseCase>();
        services.AddScoped<IUpdateFosterCarerUseCase, UpdateFosterCarerUseCase>();
        services.AddScoped<IDeleteFosterCarerUseCase, DeleteFosterCarerUseCase>();
        services.AddScoped<IDeleteFosterPartnerUseCase, DeleteFosterPartnerUseCase>();
        services.AddScoped<ISearchFosterFamiliesUseCase, SearchFosterFamiliesUseCase>();
        services.AddScoped<IGetFosterChildUseCase, GetFosterChildUseCase>();
        services.AddScoped<ICreateFosterChildUseCase, CreateFosterChildUseCase>();
        services.AddScoped<IUpdateFosterChildUseCase, UpdateFosterChildUseCase>();
        services.AddScoped<IReconfirmFosterChildUseCase, ReconfirmFosterChildUseCase>();
        services.AddScoped<IPreviewReconfirmFosterChildUseCase, PreviewReconfirmFosterChildUseCase>();
        services.AddScoped<IDeleteFosterChildUseCase, DeleteFosterChildUseCase>();
        services.AddScoped<ITemporaryCodeUploadUseCase, TemporaryCodeUploadUseCase>();
        services.AddScoped<IGetTemporaryCodeUploadsUseCase, GetTemporaryCodeUploadsUseCase>();
        services.AddScoped<IGetWorkingFamiliesCorrectionUseCase, GetWorkingFamiliesCorrectionUseCase>();
        services.AddScoped<IUpdateWorkingFamiliesCorrectionUseCase, UpdateWorkingFamiliesCorrectionUseCase>();
        services.AddScoped<IGetCheckWorkingFamiliesUseCase, GetCheckWorkingFamiliesItemUseCase>();

        return services;
    }
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
        }).ConfigurePrimaryHttpMessageHandler(() =>
        {

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

    public static IServiceCollection AddTestFactories(this IServiceCollection services, IConfiguration configuration)
    {

        // Test Data Configuration and Providers
        var testDataConfig = TestDataConfiguration.CreateFromConfiguration(configuration);
        services.AddSingleton(testDataConfig);
        services.AddSingleton<IStandardCheckTestScenarioFactory, StandardCheckTestScenarioFactory>();
        services.AddSingleton<IWorkingFamiliesTestScenarioFactory, WorkingFamiliesTestScenarioFactory>();

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

}