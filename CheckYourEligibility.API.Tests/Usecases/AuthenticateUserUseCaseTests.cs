using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class AuthenticateUserUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<AuthenticateUserUseCase>>();
        _jwtSettings = new JwtSettings
        {
            Key = "test_key_12345678901234567890123456789012",
            Issuer = "test_issuer",
            Clients = new Dictionary<string, ClientSettings>
            {
                ["test_client"] = new()
                {
                    Secret = "correct_password",
                    Scope = "read write admin"
                }
            }
        };
        _sut = new AuthenticateUserUseCase(_mockAuditGateway.Object, _mockLogger.Object, _jwtSettings);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<AuthenticateUserUseCase>> _mockLogger;
    private JwtSettings _jwtSettings;
    private AuthenticateUserUseCase _sut;
    private Fixture _fixture;

    [Test]
    public async Task AuthenticateUser_Should_Return_JwtAuthResponse_When_Successful()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        result.Should().NotBeNull();
        result.access_token.Should().NotBeNullOrEmpty();
        result.expires_in.Should().BeGreaterThan(0);
        result.token_type.Should().Be("Bearer");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidClientException_When_Authentication_Fails()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "wrong_password"
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<InvalidClientException>()
            .WithMessage("Invalid client credentials");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidClientException_When_User_Not_Found()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "unknown_user",
            client_secret = "any_password"
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<InvalidClientException>()
            .WithMessage("The client authentication failed");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_ServerErrorException_When_Key_Is_Empty()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _jwtSettings.Key = "";

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<ServerErrorException>()
            .WithMessage("The authorization server is misconfigured. Key is required.");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_ServerErrorException_When_Issuer_Is_Empty()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _jwtSettings.Issuer = "";

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<ServerErrorException>()
            .WithMessage("The authorization server is misconfigured. Issuer is required.");
    }

    [Test]
    public async Task AuthenticateUser_Should_Audit_When_User_Is_Authenticated()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        result.Should().NotBeNull();
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null), Times.Once);
    }

    [Test]
    public async Task AuthenticateUser_Should_Return_JwtAuthResponse_When_Successful_Using_ClientCredentials()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        result.Should().NotBeNull();
        result.access_token.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidClientException_When_Authentication_Fails_Using_ClientCredentials()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "wrong_password"
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<InvalidClientException>()
            .WithMessage("Invalid client credentials");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidRequestException_When_No_Valid_Credentials()
    {
        // Arrange
        var login = new SystemUser(); // No credentials set

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<InvalidRequestException>()
            .WithMessage("Either client_id/client_secret pair or Username/Password pair must be provided");
    }

    [Test]
    public async Task AuthenticateUser_Should_Include_scope_Claim_When_scope_Is_Provided()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password",
            scope = "read write"
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        result.Should().NotBeNull();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.access_token);

        token.Claims.FirstOrDefault(c => c.Type == "scope")?.Value.Should().Be("read write");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidScopeException_When_Requested_scopes_Are_Not_Allowed()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password",
            scope = "read delete" // delete is not allowed
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);
        act.Should().ThrowAsync<InvalidScopeException>()
            .WithMessage("The requested scope is invalid, unknown, or exceeds the scope granted by the resource owner");
    }

    [Test]
    public async Task AuthenticateUser_Should_Generate_Token_With_Expected_Claims()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        result.Should().NotBeNull();

        // Decode token to verify claims
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.access_token);

        // Check expected claims are present
        token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value.Should().Be("test_client");
        token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Should().NotBeNull();

        // Check token properties
        token.Issuer.Should().Be("test_issuer");
        token.Audiences.Should().Contain("test_issuer");

        // Verify expiration is set to 120 minutes from now
        var expectedExpiry = DateTime.UtcNow.AddMinutes(120);
        token.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task AuthenticateUser_Should_Log_Warning_But_Continue_When_GrantType_Is_Invalid()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password",
            grant_type = "invalid_grant_type" // Using an invalid grant type
        };

        _mockAuditGateway
            .Setup(a => a.CreateAuditEntry(AuditType.Client, login.client_id,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(login);

        // Assert
        // Verify we still got a valid response despite invalid grant type
        result.Should().NotBeNull();
        result.access_token.Should().NotBeNullOrEmpty();

        // Verify the warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Unsupported grant_type: {login.grant_type}")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidScopeException_When_Client_Has_No_Allowed_Scopes_Configured()
    {
        // Arrange
        // Create a client with no scopes configured
        var clientIdWithoutScopes = "client_without_scopes";
        var clientSecret = "some_secret";

        _jwtSettings.Clients[clientIdWithoutScopes] = new ClientSettings
        {
            Secret = clientSecret,
            Scope = null // No scopes configured for this client
        };

        var login = new SystemUser
        {
            client_id = clientIdWithoutScopes,
            client_secret = clientSecret,
            scope = "read write" // Requesting scopes that aren't defined
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);

        // Should throw InvalidScopeException with the specific error message
        act.Should().ThrowAsync<InvalidScopeException>()
            .WithMessage("Client is not authorized for any scopes");

        // Verify the error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString().Contains($"Allowed scopes not found for client: {clientIdWithoutScopes}")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void AuthenticateUser_Should_Throw_ServerErrorException_When_Token_Generation_Fails()
    {
        // Arrange
        var login = new SystemUser
        {
            client_id = "test_client",
            client_secret = "correct_password"
        };

        // Set up a scenario that would cause token generation to fail
        // An invalid key length will cause the JWT token generation to fail
        _jwtSettings.Key = "too_short_key";

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);

        // Should throw ServerErrorException with the specific error message
        act.Should().ThrowAsync<ServerErrorException>()
            .WithMessage("The authorization server encountered an unexpected error");
    }

    [Test]
    public void AuthenticateUser_Should_Throw_InvalidClientException_When_Secret_Not_Found_For_Valid_Identifier()
    {
        // Arrange
        // Create a malformed client entry where the identifier exists but has a null secret
        var userWithNoSecret = "client_with_no_secret";

        // Add the user to the dictionary but with null secret
        _jwtSettings.Clients[userWithNoSecret] = new ClientSettings { Secret = null };

        var login = new SystemUser
        {
            client_id = userWithNoSecret,
            client_secret = "any_password"
        };

        // Act & Assert
        Func<Task> act = async () => await _sut.Execute(login);

        // Should throw InvalidClientException with the correct message
        act.Should().ThrowAsync<InvalidClientException>()
            .WithMessage("The client authentication failed");

        // Verify the error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o.ToString().Contains($"Authentication secret not found for identifier: {userWithNoSecret}")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Once);
    }

    [Test]
    public void ValidateScopes_Should_Return_False_When_RequestedScopes_Provided_But_AllowedScopes_Empty()
    {
        // This test uses reflection to access the private ValidateScopes method
        // Arrange
        var requestedScopes = "read write";
        string allowedScopes = null; // No allowed scopes

        // Act
        var method = typeof(AuthenticateUserUseCase).GetMethod("ValidateScopes",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method.Invoke(null, new object[] { requestedScopes, allowedScopes });

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateScopes_Should_Return_True_When_RequestedScopes_Is_Empty()
    {
        // This test uses reflection to access the private ValidateScopes method
        // Arrange
        var requestedScopes = ""; // Empty requested scopes
        string allowedScopes = "read write"; // Properly configured allowed scopes

        // Act
        var method = typeof(AuthenticateUserUseCase).GetMethod("ValidateScopes",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method.Invoke(null, new object[] { requestedScopes, allowedScopes });

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Return_True_When_RequestedScopes_Is_Default()
    {
        // This test uses reflection to access the private ValidateScopes method
        // Arrange
        var requestedScopes = "default"; // Default scope
        string allowedScopes = "read write"; // Properly configured allowed scopes

        // Act
        var method = typeof(AuthenticateUserUseCase).GetMethod("ValidateScopes",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method.Invoke(null, new object[] { requestedScopes, allowedScopes });

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Return_False_When_RequestedScope_Not_In_AllowedScopes()
    {
        // This test uses reflection to access the private ValidateScopes method
        // Arrange
        var requestedScopes = "read write delete"; // Requesting scopes including 'delete'
        var allowedScopes = "read write"; // Only 'read' and 'write' are allowed

        // Act
        var method = typeof(AuthenticateUserUseCase).GetMethod("ValidateScopes",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method.Invoke(null, new object[] { requestedScopes, allowedScopes });

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateScopes_Should_Return_True_When_All_RequestedScopes_In_AllowedScopes()
    {
        // Arrange
        var requestedScopes = "read write"; // Requesting 'read' and 'write'
        var allowedScopes = "read write admin"; // All requested scopes are allowed

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    } // Helper method for scope validation testing

    private static bool ValidateScopes(string requestedScopes, string allowedScopes)
    {
        var method = typeof(AuthenticateUserUseCase).GetMethod("ValidateScopes",
            BindingFlags.NonPublic | BindingFlags.Static);

        return method != null && method.Invoke(null, new object[] { requestedScopes, allowedScopes }) is bool result &&
               result;
    }

    // Helper method to test the IsScopeValid method directly
    private static bool IsScopeValid(string requestedScope, string[] allowedScopesList)
    {
        var method = typeof(AuthenticateUserUseCase).GetMethod("IsScopeValid",
            BindingFlags.NonPublic | BindingFlags.Static);

        return method != null &&
               method.Invoke(null, new object[] { requestedScope, allowedScopesList }) is bool result && result;
    }

    // Helper method to test the IsSpecificScopeIdValid method directly
    private static bool IsSpecificScopeIdValid(string requestedScope, string[] allowedScopesList)
    {
        var method = typeof(AuthenticateUserUseCase).GetMethod("IsSpecificScopeIdValid",
            BindingFlags.NonPublic | BindingFlags.Static);

        return method != null &&
               method.Invoke(null, new object[] { requestedScope, allowedScopesList }) is bool result && result;
    }

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_LocalAuthorityWithId_And_AllowedScope_Is_LocalAuthority()
    {
        // appsettings has "local_authority check" and user logs in with "local_authority:xx"

        // Arrange
        var requestedScopes = "local_authority:99 check";
        var allowedScopes = "local_authority check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_LocalAuthorityWithId_And_AllowedScope_Has_SameId()
    {
        // appsettings has "local_authority:xx check" and user logs in with "local_authority:xx"

        // Arrange
        var requestedScopes = "local_authority:99";
        var allowedScopes = "local_authority:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_False_When_RequestedScope_Is_LocalAuthority_And_AllowedScope_Has_SpecificId()
    {
        // appsettings has "local_authority:xx check" and user logs in with "local_authority"

        // Arrange
        var requestedScopes = "local_authority";
        var allowedScopes = "local_authority:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_LocalAuthority_And_AllowedScope_Is_LocalAuthority()
    {
        // appsettings has "local_authority check" and user logs in with "local_authority"

        // Arrange
        var requestedScopes = "local_authority";
        var allowedScopes = "local_authority check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Return_False_When_RequestedScope_Is_LocalAuthorityWithDifferentId()
    {
        // appsettings has "local_authority:99 check" but user logs in with "local_authority:88"

        // Arrange
        var requestedScopes = "local_authority:88";
        var allowedScopes = "local_authority:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateScopes_Should_Handle_Multiple_Scopes_Correctly()
    {
        // Arrange
        var requestedScopes = "local_authority:99 check application";
        var allowedScopes = "local_authority:99 check application admin";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Return_False_When_Any_RequestedScope_Is_Not_Allowed()
    {
        // Arrange
        var requestedScopes = "local_authority:99 check unauthorized_scope";
        var allowedScopes = "local_authority:99 check application";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateScopes_Should_Handle_Multiple_LocalAuthority_Scopes_Correctly()
    {
        // Arrange
        var requestedScopes = "local_authority:99 check";
        var allowedScopes = "local_authority:99 local_authority:88 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Allow_LocalAuthorityWithAnyId_When_LocalAuthority_Is_Allowed()
    {
        // Arrange
        var requestedScopes = "local_authority:123 check";
        var allowedScopes = "local_authority check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Handle_Empty_Values_Correctly()
    {
        // Arrange & Act
        var result1 = ValidateScopes("", "local_authority check");
        var result2 = ValidateScopes("local_authority", "");

        // Assert
        result1.Should().BeTrue(); // Empty requested scope should be valid
        result2.Should().BeFalse(); // Valid requested scope but empty allowed scope should be invalid
    }

    [Test]
    public void IsScopeValid_Should_Return_True_For_Direct_Match()
    {
        // Arrange
        var requestedScope = "read";
        var allowedScopesList = new[] { "read", "write" };

        // Act
        var result = IsScopeValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsScopeValid_Should_Handle_LocalAuthority_Generic_Scope_Correctly()
    {
        // Arrange
        var requestedScope = "local_authority";
        var allowedScopesList = new[] { "local_authority", "read" };

        // Act
        var result = IsScopeValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsScopeValid_Should_Return_False_When_Requesting_Generic_But_Only_Specific_Is_Allowed()
    {
        // Arrange
        var requestedScope = "local_authority";
        var allowedScopesList = new[] { "local_authority:99", "read" };

        // Act
        var result = IsScopeValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_True_When_Generic_LA_Is_Allowed()
    {
        // Arrange
        var requestedScope = "local_authority:99";
        var allowedScopesList = new[] { "local_authority", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_True_When_Same_Id_LA_Is_Allowed()
    {
        // Arrange
        var requestedScope = "local_authority:99";
        var allowedScopesList = new[] { "local_authority:99", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_False_When_Different_Id_LA_Is_Requested()
    {
        // Arrange
        var requestedScope = "local_authority:99";
        var allowedScopesList = new[] { "local_authority:88", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeFalse();
    }

    //MAT tests

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_MultiAcademyTrustWithId_And_AllowedScope_Is_LocalAuthority()
    {
        // appsettings has "multi_academy_trust check" and user logs in with "multi_academy_trust:xx"

        // Arrange
        var requestedScopes = "multi_academy_trust:99 check";
        var allowedScopes = "multi_academy_trust check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_MultiAcademyTrustWithId_And_AllowedScope_Has_SameId()
    {
        // appsettings has "multi_academy_trust:xx check" and user logs in with "multi_academy_trust:xx"

        // Arrange
        var requestedScopes = "multi_academy_trust:99";
        var allowedScopes = "multi_academy_trust:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_False_When_RequestedScope_Is_MultiAcademyTrust_And_AllowedScope_Has_SpecificId()
    {
        // appsettings has "multi_academy_trust:xx check" and user logs in with "multi_academy_trust"

        // Arrange
        var requestedScopes = "multi_academy_trust";
        var allowedScopes = "multi_academy_trust:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void
        ValidateScopes_Should_Return_True_When_RequestedScope_Is_MultiAcademyTrust_And_AllowedScope_Is_LocalAuthority()
    {
        // appsettings has "multi_academy_trust check" and user logs in with "multi_academy_trust"

        // Arrange
        var requestedScopes = "multi_academy_trust";
        var allowedScopes = "multi_academy_trust check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Return_False_When_RequestedScope_Is_MultiAcademyTrustWithDifferentId()
    {
        // appsettings has "multi_academy_trust:99 check" but user logs in with "multi_academy_trust:88"

        // Arrange
        var requestedScopes = "multi_academy_trust:88";
        var allowedScopes = "multi_academy_trust:99 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateScopes_Should_Handle_Multiple_MultiAcademyTrust_Scopes_Correctly()
    {
        // Arrange
        var requestedScopes = "multi_academy_trust:99 check";
        var allowedScopes = "multi_academy_trust:99 multi_academy_trust:88 check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void ValidateScopes_Should_Allow_MultiAcademyTrustWithAnyId_When_MultiAcademyTrust_Is_Allowed()
    {
        // Arrange
        var requestedScopes = "multi_academy_trust:123 check";
        var allowedScopes = "multi_academy_trust check";

        // Act
        var result = ValidateScopes(requestedScopes, allowedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsScopeValid_Should_Handle_MultiAcademyTrust_Generic_Scope_Correctly()
    {
        // Arrange
        var requestedScope = "multi_academy_trust";
        var allowedScopesList = new[] { "multi_academy_trust", "read" };

        // Act
        var result = IsScopeValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_True_When_Generic_MAT_Is_Allowed()
    {
        // Arrange
        var requestedScope = "multi_academy_trust:99";
        var allowedScopesList = new[] { "multi_academy_trust", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_True_When_Same_Id_MAT_Is_Allowed()
    {
        // Arrange
        var requestedScope = "multi_academy_trust:99";
        var allowedScopesList = new[] { "multi_academy_trust:99", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void IsSpecificScopeIdValid_Should_Return_False_When_Different_Id_MAT_Is_Requested()
    {
        // Arrange
        var requestedScope = "multi_academy_trust:99";
        var allowedScopesList = new[] { "multi_academy_trust:88", "read" };

        // Act
        var result = IsSpecificScopeIdValid(requestedScope, allowedScopesList);

        // Assert
        result.Should().BeFalse();
    }

}