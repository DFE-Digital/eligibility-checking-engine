using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
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
        FluentActions
            .Invoking(async () => await _sut.Execute(
                null!,
                new List<int> { 1 },
                1,
                Guid.NewGuid(),
                DateTime.UtcNow))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_CarerId_Is_Empty()
    {
        var req = new FosterChildRequest
        {
            ChildFirstName = "Child",
            ChildLastName = "One",
            ChildDateOfBirth = DateTime.UtcNow,
            ChildPostCode = "AB1 2CD"
        };

        FluentActions
            .Invoking(async () => await _sut.Execute(
                req,
                new List<int> { 1 },
                1,
                Guid.Empty,
                DateTime.UtcNow))
            .Should()
            .ThrowAsync<ValidationException>();
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_User_Does_Not_Have_LA_Access()
    {
        var req = new FosterChildRequest
        {
            ChildFirstName = "Child",
            ChildLastName = "One",
            ChildDateOfBirth = DateTime.UtcNow,
            ChildPostCode = "AB1 2CD"
        };

        FluentActions
            .Invoking(async () => await _sut.Execute(
                req,
                new List<int> { 999 },
                1,
                Guid.NewGuid(),
                DateTime.UtcNow))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_Request_Is_Invalid()
    {
        var req = new FosterChildRequest
        {
            ChildFirstName = "",
            ChildLastName = "",
            ChildPostCode = ""
        };

        FluentActions
            .Invoking(async () => await _sut.Execute(
                req,
                new List<int> { 1 },
                1,
                Guid.NewGuid(),
                DateTime.UtcNow))
            .Should()
            .ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var req = new FosterChildRequest
        {
            ChildFirstName = "Child",
            ChildLastName = "One",
            ChildDateOfBirth = DateTime.UtcNow,
            ChildPostCode = "AB1 2CD"
        };

        var carerId = Guid.NewGuid();

        var expected = new FosterChildCreatedResponse
        {
            ChildName = "Child One",
            EligiblityCode = "X1",
            Status = "Active",
            EligibilityConfirmed = DateTime.UtcNow.ToString(),
            GracePeriodEndDate = DateTime.UtcNow.ToString()
        };

        _mockGateway
            .Setup(x => x.CreateFosterChild(
                req,
                1,
                carerId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            req,
            new List<int> { 1 },
            1,
            carerId,
            DateTime.UtcNow);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task Execute_Should_Allow_Global_Access_When_LA_List_Contains_Zero()
    {
        // Arrange
        var req = new FosterChildRequest
        {
            ChildFirstName = "Child",
            ChildLastName = "One",
            ChildDateOfBirth = DateTime.UtcNow,
            ChildPostCode = "AB1 2CD"
        };

        var carerId = Guid.NewGuid();

        var expected = new FosterChildCreatedResponse
        {
            ChildName = "Child One",
            EligiblityCode = "X1",
            Status = "Active"
        };

        _mockGateway
            .Setup(x => x.CreateFosterChild(
                req,
                123,
                carerId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            req,
            new List<int> { 0 },
            123,
            carerId,
            DateTime.UtcNow);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}