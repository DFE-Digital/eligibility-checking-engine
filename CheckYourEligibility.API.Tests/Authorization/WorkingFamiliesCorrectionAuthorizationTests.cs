using System.Security.Claims;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckYourEligibility.API.Tests.Authorization;

[TestFixture]
public class WorkingFamiliesCorrectionAuthorizationTests
{
    [TestCase(UserType.EligibilityCheckingEngineSupport,
    UserRoleName.Support_CodeManagement, true, true)]
    [TestCase(UserType.EligibilityCheckingEngineSupport,
    UserRoleName.Support_UserManagement, true, false)]
    [TestCase(UserType.EligibilityCheckingEngineSupport,
    null, true, false)]
    [TestCase(UserType.ChildcareAdmin,
    UserRoleName.Support_CodeManagement, true, false)]
    [TestCase(UserType.API,
    UserRoleName.Support_CodeManagement, true, false)]
    [TestCase(null,
    UserRoleName.Support_CodeManagement, true, false)]
    [TestCase(UserType.EligibilityCheckingEngineSupport,
    UserRoleName.Support_CodeManagement, false, false)]
    [TestCase(null, null, false, false)]
    public async Task Correction_access_requires_authenticated_support_user_with_code_management_role(
    UserType? userType,
    UserRoleName? role,
    bool authenticated,
    bool expected)
    {
        // Arrange: register the application's actual policies and handlers.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var claims = new List<Claim>();

        if (userType.HasValue)
            claims.Add(new Claim("UserType", userType.Value.ToString()));

        if (role.HasValue)
            claims.Add(new Claim(ClaimTypes.Role, role.Value.ToString()));

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: authenticated ? "Test" : null);

        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await authorization.AuthorizeAsync(
            user,
            resource: null,
            policyName: PolicyNames.ManageWorkingFamiliesCorrections);

        // Assert
        result.Succeeded.Should().Be(expected);
    }
}