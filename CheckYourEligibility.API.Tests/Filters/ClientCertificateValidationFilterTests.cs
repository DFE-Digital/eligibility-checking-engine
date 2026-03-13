using CheckYourEligibility.API.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography.X509Certificates;

namespace CheckYourEligibility.API.Tests.Filters;

[TestFixture]
public class ClientCertificateValidationFilterTests
{
    private Mock<ILogger<ClientCertificateValidationFilter>> _mockLogger = null!;

    private static X509Certificate2 CreateTestCert(string cn)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={cn}", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    private static IConfiguration BuildConfig(
        bool enabled,
        string? expectedCN = null,
        string? expectedIssuer = null,
        string? expectedSerial = null,
        string? expectedThumbprint = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Ecs:EligibilityEvents:InboundCertificate:Enabled"] = enabled.ToString(),
            ["Ecs:EligibilityEvents:InboundCertificate:ExpectedCN"] = expectedCN ?? "",
            ["Ecs:EligibilityEvents:InboundCertificate:ExpectedIssuer"] = expectedIssuer ?? "",
            ["Ecs:EligibilityEvents:InboundCertificate:ExpectedSerialNumber"] = expectedSerial ?? "",
            ["Ecs:EligibilityEvents:InboundCertificate:ExpectedThumbprint"] = expectedThumbprint ?? "",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static ActionExecutingContext CreateContext(X509Certificate2? cert)
    {
        var httpContext = new DefaultHttpContext();
        if (cert != null)
        {
            httpContext.Connection.ClientCertificate = cert;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ClientCertificateValidationFilter>>();
    }

    [Test]
    public async Task ShouldProceed_WhenValidationDisabled()
    {
        // Arrange
        var config = BuildConfig(enabled: false);
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert: null);
        var nextCalled = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturn403_WhenNoCertPresented()
    {
        // Arrange
        var config = BuildConfig(enabled: true, expectedCN: "ECS-30H-Interface-PreProd");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert: null);

        // Act
        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ShouldProceed_WhenCNMatches()
    {
        // Arrange
        var cert = CreateTestCert("ECS-30H-Interface-PreProd");
        var config = BuildConfig(enabled: true, expectedCN: "ECS-30H-Interface-PreProd");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);
        var nextCalled = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturn403_WhenCNDoesNotMatch()
    {
        // Arrange
        var cert = CreateTestCert("WrongCN");
        var config = BuildConfig(enabled: true, expectedCN: "ECS-30H-Interface-PreProd");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);

        // Act
        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ShouldProceed_WhenThumbprintMatches()
    {
        // Arrange
        var cert = CreateTestCert("TestCert");
        var config = BuildConfig(enabled: true, expectedThumbprint: cert.Thumbprint);
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);
        var nextCalled = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturn403_WhenThumbprintDoesNotMatch()
    {
        // Arrange
        var cert = CreateTestCert("TestCert");
        var config = BuildConfig(enabled: true, expectedThumbprint: "AABBCCDDEE0011223344");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);

        // Act
        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ShouldReturn403_WhenSerialNumberDoesNotMatch()
    {
        // Arrange
        var cert = CreateTestCert("TestCert");
        var config = BuildConfig(enabled: true, expectedSerial: "DEADBEEF");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);

        // Act
        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ShouldProceed_WhenMultipleFieldsAllMatch()
    {
        // Arrange
        var cert = CreateTestCert("ECS-30H-Interface-PreProd");
        var config = BuildConfig(
            enabled: true,
            expectedCN: "ECS-30H-Interface-PreProd",
            expectedThumbprint: cert.Thumbprint);
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);
        var nextCalled = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturn403_WhenCNMatchesButThumbprintDoesNot()
    {
        // Arrange
        var cert = CreateTestCert("ECS-30H-Interface-PreProd");
        var config = BuildConfig(
            enabled: true,
            expectedCN: "ECS-30H-Interface-PreProd",
            expectedThumbprint: "WRONGTHUMBPRINT");
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);

        // Act
        await filter.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        // Assert
        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ShouldProceed_WhenEnabledButNoFieldsConfigured()
    {
        // Arrange — enabled but all expected fields are empty → no checks to fail → pass through
        var cert = CreateTestCert("AnyCert");
        var config = BuildConfig(enabled: true);
        var filter = new ClientCertificateValidationFilter(config, _mockLogger.Object);
        var context = CreateContext(cert);
        var nextCalled = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }
}
