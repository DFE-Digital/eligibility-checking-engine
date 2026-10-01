using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateFosterCarerUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private UpdateFosterCarerUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new UpdateFosterCarerUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Carer_Id_Is_Empty()
    {
        // Arrange
        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = BuildValidCarerRequest()
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
    public async Task Execute_Should_Throw_ValidationException_When_Carer_Request_Is_Invalid()
    {
        // Arrange
        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = new FosterCarerRequest
            {
                CarerFirstName = string.Empty,
                CarerLastName = "Bloggs",
                CarerDateOfBirth = new DateTime(1980, 1, 1),
                CarerNationalInsuranceNumber = "BAD"
            }
        };

        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), 201, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_With_Updated_Request()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();
        var request = new UpdateFosterCarerRequest
        {
            FosterCarerRequest = BuildValidCarerRequest(),
            FosterPartnerRequest = BuildValidPartnerRequest()
        };

        _mockGateway
            .Setup(x => x.UpdateFosterCarer(fosterCarerId, 201, request))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(fosterCarerId, 201, request);

        // Assert
        _mockGateway.Verify(x => x.UpdateFosterCarer(fosterCarerId, 201, request), Times.Once);
    }

    private static FosterCarerRequest BuildValidCarerRequest()
    {
        return new FosterCarerRequest
        {
            CarerFirstName = "Joe",
            CarerLastName = "Bloggs",
            CarerDateOfBirth = new DateTime(1980, 1, 1),
            CarerNationalInsuranceNumber = "AB123456C"
        };
    }

    private static FosterPartnerRequest BuildValidPartnerRequest()
    {
        return new FosterPartnerRequest
        {
            PartnerFirstName = "Jane",
            PartnerLastName = "Bloggs",
            PartnerDateOfBirth = new DateTime(1982, 1, 1),
            PartnerNationalInsuranceNumber = "AB123456C"
        };
    }
}
