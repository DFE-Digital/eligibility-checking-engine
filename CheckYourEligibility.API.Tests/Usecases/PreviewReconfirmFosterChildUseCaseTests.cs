using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class PreviewReconfirmFosterChildUseCaseTests
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private PreviewReconfirmFosterChildUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new PreviewReconfirmFosterChildUseCase(_mockGateway.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGateway.VerifyAll();
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
    public async Task Execute_Should_Throw_ValidationException_When_Request_Is_Invalid()
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
    public async Task Execute_Should_Call_Gateway_And_Return_Preview_Response()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();
        var request = new FosterChildReconfirmRequest
        {
            EligibilityCode = "40000000001",
            SubmissionDate = new DateTime(2026, 9, 15)
        };

        var expected = new FosterCodePreviewResponse
        {
            ValidityStartDate = new DateTime(2026, 9, 15),
            TermValidity = new TermValidity(
                new Term(TermName.Spring, new DateTime(2026, 1, 1), new DateTime(2026, 3, 30)),
                new Term(TermName.Summer, new DateTime(2026, 4, 1), new DateTime(2026, 8, 31))
            ),
            ReconfirmationProperties = new ReconfirmationProperties
            {
                StartDate = new DateTime(2026, 9, 15),
                EndDate = new DateTime(2027, 1, 15)
            },
            GracePeriodEndDate = new DateTime(2027, 3, 31),
            ChildTooYoung = false
        };

        _mockGateway
            .Setup(x => x.PreviewReconfirmFosterChild(fosterChildId, 201, request.SubmissionDate))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(fosterChildId, request, 201);

        // Assert
        result.Should().BeSameAs(expected);
    }
}
