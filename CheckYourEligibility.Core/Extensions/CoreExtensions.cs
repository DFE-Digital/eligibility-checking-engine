using CheckYourEligibility.Core.Adapters;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using CheckYourEligibility.Core.Database;

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
        services.AddTransient<IWorkingFamiliesEvent, WorkingFamiliesEventGateway>();
        services.AddTransient<IMultiAcademyTrust, MultiAcademyTrustGateway>();
        services.AddTransient<IEligibilityPolicy, EligibilityPolicyGateway>();
        services.AddTransient<IFosterFamilies, FosterFamiliesGateway>();
        return services;
    }

}