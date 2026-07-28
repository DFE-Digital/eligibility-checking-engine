using System;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private GetFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new GetFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_When_Id_Is_Empty()
    {
        FluentActions.Invoking(async () => await _sut.Execute(Guid.Empty)).Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        var id = Guid.NewGuid();
        var expected = new FosterChildResponse { FosterChildId = id, ChildFullName = "Child One", ChildDateOfBirth = DateTime.UtcNow };

        _mockGateway.Setup(x => x.GetFosterChild(id, true)).ReturnsAsync(expected);

        var result = await _sut.Execute(id, true);

        result.Should().BeEquivalentTo(expected);
    }
}