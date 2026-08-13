using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

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
    public async Task Execute_Should_Throw_When_Id_Is_Empty()
    {
        // Arrange
        
        // Act
        var act = () => _sut.Execute(
            Guid.Empty,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Throw_When_User_Has_No_Access_To_LocalAuthority()
    {
        // Arrange
        var id = Guid.NewGuid();

        _mockGateway
            .Setup(x => x.GetFosterChild(
                id,
                1,
                false))
            .ThrowsAsync(new UnauthorizedAccessException("User does not have access to this local authority"));

        // Act
        var act = () => _sut.Execute(
            id,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var id = Guid.NewGuid();

        var expected = new FosterChildResponse
        {
            FosterChildId = id,
            ChildFullName = "Child One",
            ChildDateOfBirth = DateTime.UtcNow
        };

        _mockGateway
            .Setup(x => x.GetFosterChild(
                id,
                1,
                true))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            id,
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

        var expected = new FosterChildResponse
        {
            FosterChildId = id
        };

        _mockGateway
            .Setup(x => x.GetFosterChild(
                id,
                123,
                false))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            id,
            123);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task Execute_Should_Throw_When_Gateway_Returns_Null()
    {
        // Arrange
        var id = Guid.NewGuid();

        _mockGateway
            .Setup(x => x.GetFosterChild(
                id,
                1,
                false))
            .ReturnsAsync((FosterChildResponse?)null);

        // Act
        var act = () => _sut.Execute(
            id,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<KeyNotFoundException>();
    }
}