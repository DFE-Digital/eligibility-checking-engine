using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ReconfirmFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private ReconfirmFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new ReconfirmFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
    }

    [Test]
    public async Task Execute_Should_Forward_Request_To_Gateway_And_Return_Response()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();
        const int localAuthorityId = 201;
        var request = new FosterChildReconfirmRequest
        {
            EligibilityCode = "40000000001",
            SubmissionDate = new DateTime(2026, 9, 15)
        };
        var expected = new FosterChildResponse { FosterChildId = fosterChildId };

        _mockGateway
            .Setup(x => x.ReconfirmFosterChild(fosterChildId, localAuthorityId, request.SubmissionDate))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(fosterChildId, request, localAuthorityId);

        // Assert
        result.Should().BeSameAs(expected);
    }

    [Test]
    public async Task Execute_Should_Throw_When_Request_Is_Null()
    {
        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), null!, 201);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_EligibilityCode_Is_Missing()
    {
        // Arrange
        var request = new FosterChildReconfirmRequest
        {
            EligibilityCode = string.Empty,
            SubmissionDate = new DateTime(2026, 9, 15)
        };

        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), request, 201);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_SubmissionDate_Is_Missing()
    {
        // Arrange
        var request = new FosterChildReconfirmRequest
        {
            EligibilityCode = "40000000001",
            SubmissionDate = default
        };

        // Act
        Func<Task> act = () => _sut.Execute(Guid.NewGuid(), request, 201);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}