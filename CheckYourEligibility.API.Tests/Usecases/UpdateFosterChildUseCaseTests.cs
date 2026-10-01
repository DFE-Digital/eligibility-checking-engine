using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private UpdateFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new UpdateFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Child_Id_Is_Empty()
    {
        // Arrange
        var request = new UpdateFosterChildRequest
        {
            FosterChildRequest = BuildValidChildRequest()
        };

        // Act
        Func<Task> act = () => _sut.Execute(Guid.Empty, 201, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Request_Is_Null()
    {
        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), 201, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_Child_Request_Is_Invalid()
    {
        // Arrange
        var request = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest
            {
                ChildFirstName = string.Empty,
                ChildLastName = "Bloggs",
                ChildDateOfBirth = new DateTime(2015, 1, 1),
                ChildPostCode = "SW1A 1AA"
            }
        };

        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), 201, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Return_Gateway_Response_When_Valid()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();
        var request = new UpdateFosterChildRequest
        {
            FosterChildRequest = BuildValidChildRequest()
        };
        var expected = new FosterChildResponse
        {
            FosterChildId = fosterChildId,
            ChildFirstName = "Alice",
            ChildLastName = "Bloggs",
            ChildDateOfBirth = new DateTime(2015, 2, 2),
            ChildPostCode = "SW1A 1AA"
        };

        _mockGateway
            .Setup(x => x.UpdateFosterChild(fosterChildId, 201, request))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(fosterChildId, 201, request);

        // Assert
        result.Should().BeEquivalentTo(expected);
        _mockGateway.Verify(x => x.UpdateFosterChild(fosterChildId, 201, request), Times.Once);
    }

    private static FosterChildRequest BuildValidChildRequest()
    {
        return new FosterChildRequest
        {
            ChildFirstName = "Alice",
            ChildLastName = "Bloggs",
            ChildDateOfBirth = new DateTime(2015, 2, 2),
            ChildPostCode = "SW1A 1AA"
        };
    }
}
