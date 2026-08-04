using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
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
        FluentActions
            .Invoking(async () => await _sut.Execute(
                Guid.Empty,
                1))
            .Should()
            .ThrowAsync<ValidationException>();
    }

    [Test]
    public void Execute_Should_Throw_When_User_Has_No_Access_To_LocalAuthority()
    {
        FluentActions
            .Invoking(async () => await _sut.Execute(
                Guid.NewGuid(),
                1))
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
    public void Execute_Should_Throw_When_Gateway_Returns_Null()
    {
        var id = Guid.NewGuid();

        _mockGateway
            .Setup(x => x.GetFosterChild(
                id,
                1,
                false))
            .ReturnsAsync((FosterChildResponse?)null);

        FluentActions
            .Invoking(async () => await _sut.Execute(
                id,
                1))
            .Should()
            .ThrowAsync<KeyNotFoundException>();
    }
}