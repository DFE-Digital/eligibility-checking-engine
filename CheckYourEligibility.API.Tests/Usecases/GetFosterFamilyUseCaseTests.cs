using System;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetFosterFamilyUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private GetFosterFamilyUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new GetFosterFamilyUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        var id = Guid.NewGuid();
        var expected = new FosterFamilyResponse { FosterCarerId = id, CarerFirstName = "Joe", CarerLastName = "Bloggs", CarerDateOfBirth = DateTime.UtcNow, CarerNationalInsuranceNumber = "AB123456C" };

        _mockGateway.Setup(x => x.GetFosterFamily(id, true)).ReturnsAsync(expected);

        var result = await _sut.Execute(id, true);

        result.Should().BeEquivalentTo(expected);
    }
}