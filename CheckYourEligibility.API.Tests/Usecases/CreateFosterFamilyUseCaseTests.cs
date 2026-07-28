using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateFosterFamilyUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private CreateFosterFamilyUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new CreateFosterFamilyUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_When_Request_Is_Null()
    {
        FluentActions.Invoking(async () => await _sut.Execute(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        var request = new FosterFamilyRequest { SubmissionDate = DateTime.UtcNow, FosterCarer = new FosterCarerRequest { CarerFirstName = "Joe", CarerLastName = "Bloggs", CarerDateOfBirth = DateTime.UtcNow, CarerNationalInsuranceNumber = "AB123456C" }, FosterChild = new FosterChildRequest { ChildFirstName = "Child", ChildLastName = "One", ChildDateOfBirth = DateTime.UtcNow, ChildPostCode = "AB1 2CD" } };
        var expected = new FosterFamilyCreatedResponse { ChildName = "Child One", EligiblityCode = "X1", Status = "Active", EligibilityConfirmed = DateTime.UtcNow.ToString(), GracePeriodEndDate = DateTime.UtcNow.ToString() };

        _mockGateway.Setup(x => x.CreateFosterFamily(request)).ReturnsAsync(expected);

        var result = await _sut.Execute(request);

        result.Should().BeEquivalentTo(expected);
    }
}