using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
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
    public void Execute_Should_Throw_When_Id_Is_Empty()
    {
        FluentActions
            .Invoking(async () => await _sut.Execute(
                Guid.Empty,
                new List<int> { 1 },
                1))
            .Should()
            .ThrowAsync<ValidationException>();
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_User_Does_Not_Have_LA_Access()
    {
        FluentActions
            .Invoking(async () => await _sut.Execute(
                Guid.NewGuid(),
                new List<int> { 999 },
                1))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var id = Guid.NewGuid();

        var expected = new FosterFamilyResponse
        {
            FosterCarerId = id,
            CarerFirstName = "Joe",
            CarerLastName = "Bloggs",
            CarerDateOfBirth = DateTime.UtcNow,
            CarerNationalInsuranceNumber = "AB123456C"
        };

        _mockGateway
            .Setup(x => x.GetFosterFamily(
                id,
                1,
                true))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            id,
            new List<int> { 1 },
            1,
            true);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task Execute_Should_Allow_Global_Access_When_LA_List_Contains_Zero()
    {
        // Arrange
        var id = Guid.NewGuid();

        var expected = new FosterFamilyResponse
        {
            FosterCarerId = id,
            CarerFirstName = "Joe",
            CarerLastName = "Bloggs"
        };

        _mockGateway
            .Setup(x => x.GetFosterFamily(
                id,
                123,
                false))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            id,
            new List<int> { 0 },
            123);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}