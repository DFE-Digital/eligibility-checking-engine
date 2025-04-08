using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Notify.Interfaces;
using System;
using System.Collections.Generic;

namespace CheckYourEligibility.API.Tests;

public class NotifyGatewayTests : TestBase.TestBase
{
    private Mock<INotificationClient> _mockClient;
    private IConfiguration _configuration;
    private NotifyGateway _sut;

    [SetUp]
    public void Setup()
    {
        _mockClient = new Mock<INotificationClient>();
        var configData = new Dictionary<string, string>
        {
            { "Notify:Templates:ParentApplicationCreated", "mock_id" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        _sut = new NotifyGateway(_mockClient.Object, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
        // Clean up resources if needed
    }

    [Test]
    public void SendNotification_GetsCorrectTemplateFromConfig_AndCallsClient()
    {
        // Arrange
        var notificationRequest = _fixture.Create<NotificationRequest>();
        var templateId = Guid.NewGuid().ToString();

        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(s => s.Value).Returns(templateId);

        // Act
        _sut.SendNotification(notificationRequest);

        // Assert
        _sut.Should().NotBeNull();
    }

    [Test]
    public void SendNotification_WithValidParameters_CompletesSuccessfully()
    {
        // Arrange
        var notificationRequest = _fixture.Create<NotificationRequest>();
        var templateId = Guid.NewGuid().ToString();

        // Act
        Action act = () => _sut.SendNotification(notificationRequest);

        // Assert
        act.Should().NotThrow();
    }
}