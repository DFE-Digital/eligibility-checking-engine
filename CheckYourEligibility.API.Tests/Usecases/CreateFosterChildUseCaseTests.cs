using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private CreateFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new CreateFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_When_Request_Is_Null()
    {
        FluentActions.Invoking(async () => await _sut.Execute(null!, Guid.NewGuid(), DateTime.UtcNow)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_CarerId_Is_Empty()
    {
        var req = new FosterChildRequest { ChildFirstName = "Child", ChildLastName = "One", ChildDateOfBirth = DateTime.UtcNow, ChildPostCode = "AB1 2CD" };
        FluentActions.Invoking(async () => await _sut.Execute(req, Guid.Empty, DateTime.UtcNow)).Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        var req = new FosterChildRequest { ChildFirstName = "Child", ChildLastName = "One", ChildDateOfBirth = DateTime.UtcNow, ChildPostCode = "AB1 2CD" };
        var carerId = Guid.NewGuid();
        var expected = new FosterChildCreatedResponse { ChildName = "Child One", EligiblityCode = "X1", Status = "Active", EligibilityConfirmed = DateTime.UtcNow.ToString(), GracePeriodEndDate = DateTime.UtcNow.ToString() };

        _mockGateway.Setup(x => x.CreateFosterChild(req, carerId, It.IsAny<DateTime>())).ReturnsAsync(expected);

        var result = await _sut.Execute(req, carerId, DateTime.UtcNow);

        result.Should().BeEquivalentTo(expected);
    }
}