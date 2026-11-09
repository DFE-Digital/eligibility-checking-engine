using CheckYourEligibility.API.Extensions.Authorization;
using CheckYourEligibility.Core.Domain.Authorization;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.Text;
namespace CheckYourEligibility.API;

[ExcludeFromCodeCoverage(Justification = "extension of program")]
public static class ProgramExtensions
{
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

        services.AddSingleton<IAuthorizationHandler, RuleRequirementHandler>();
        services.AddAuthorization(options =>
        {
            // API scope-based policies
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

            // User source and role-based policies
            options.AddPolicy(PolicyNames.RequireFreeSchoolMealsAdminPortalSource,
                policy =>
                {
                    policy.Requirements.Add(new RuleRequirement(
                        RuleBuilder.UserType(UserType.FreeSchoolMealsAdmin)
                    ));
                });

            options.AddPolicy(PolicyNames.RequireSupportPortalSource, policy =>
            {
                policy.Requirements.Add(RuleBuilder.UserType(UserType.EligibilityCheckingEngineSupport).Build());
            });

            options.AddPolicy(PolicyNames.RequireChildCareAdminSource, policy =>
            {
                policy.Requirements.Add(RuleBuilder.UserType(UserType.ChildcareAdmin).Build());
            });
            options.AddPolicy(PolicyNames.RequireEstablishmentManagementRole, policy =>
            {
                policy.Requirements.Add(
                    RuleBuilder.Role(UserRoleName.Support_EstablishmentManagement)
                        .Build());
            });

            // Compound authorization rules

            // Allow users to retrieve eligibility code history:
            // - If request is from the Childcare Admin portal they must have LA scope
            // - If request is from the Support portal they must have code management role
            options.AddPolicy(PolicyNames.GetEligibilityCodeHistory, policy =>
            {
                policy.Requirements.Add(
                    RuleBuilder.Or(
                        RuleBuilder.And(
                            RuleBuilder.UserType(UserType.ChildcareAdmin),
                            RuleBuilder.OrganisationType(OrganisationType.local_authority)
                        ),
                        RuleBuilder.And(
                            RuleBuilder.UserType(UserType.EligibilityCheckingEngineSupport),
                            RuleBuilder.Role(UserRoleName.Support_CodeManagement)
                        )
                    )
                    .Build());
            });

            options.AddPolicy(PolicyNames.ManageWorkingFamiliesCorrections, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(
                    RuleBuilder.And(
                        RuleBuilder.UserType(UserType.EligibilityCheckingEngineSupport),
                        RuleBuilder.Role(UserRoleName.Support_CodeManagement)
                    ).Build());
            });

            options.AddPolicy(PolicyNames.RequireUserManagementRole, policy =>
            {
                policy.Requirements.Add(
                    RuleBuilder.And(
                        RuleBuilder.UserType(UserType.EligibilityCheckingEngineSupport),
                        RuleBuilder.Role(UserRoleName.Support_UserManagement)
                    ).Build());
            });
            options.AddPolicy(PolicyNames.RequireEstablishmentManagementRole, policy =>
            {
                policy.Requirements.Add(
                    RuleBuilder.And(
                        RuleBuilder.UserType(UserType.EligibilityCheckingEngineSupport),
                        RuleBuilder.Role(UserRoleName.Support_EstablishmentManagement)
                    ).Build());
            });
        });

        return services;
    }
}