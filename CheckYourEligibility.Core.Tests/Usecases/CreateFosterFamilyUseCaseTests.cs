using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class CreateFosterFamilyUseCaseTests : TestBase
{
    private Mock<IFosterFamilies> _mockGateway = null!;
    private CreateFosterFamilyUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new CreateFosterFamilyUseCase(_mockGateway.Object);
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
                1))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_User_Does_Not_Have_LA_Access()
    {
        var request = BuildValidRequest();

        FluentActions
            .Invoking(async () => await _sut.Execute(
                request,
                1))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_Request_Is_Invalid()
    {
        var request = new FosterFamilyRequest
        {
            FosterCarer = new FosterCarerRequest(),
            FosterChild = new FosterChildRequest()
        };

        FluentActions
            .Invoking(async () => await _sut.Execute(
                request,
                1))
            .Should()
            .ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_Gateway_And_Return_Response()
    {
        // Arrange
        var request = BuildValidRequest();

        var expected = new FosterFamilyCreatedResponse
        {
            ChildName = "Child One",
            EligiblityCode = "X1",
            Status = "Active",
            EligibilityConfirmed = DateTime.UtcNow.ToString(),
            GracePeriodEndDate = DateTime.UtcNow.ToString()
        };

        _mockGateway
            .Setup(x => x.CreateFosterFamily(request))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            request,
            1);

        // Assert
        result.Should().BeEquivalentTo(expected);

        request.FosterCarer.LocalAuthorityID.Should().Be(1);
    }

    [Test]
    public async Task Execute_Should_Allow_Global_Access_When_LA_List_Contains_Zero()
    {
        // Arrange
        var request = BuildValidRequest();

        var expected = new FosterFamilyCreatedResponse
        {
            ChildName = "Child One",
            EligiblityCode = "X1",
            Status = "Active"
        };

        _mockGateway
            .Setup(x => x.CreateFosterFamily(request))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Execute(
            request,
            999);

        // Assert
        result.Should().BeEquivalentTo(expected);
        request.FosterCarer.LocalAuthorityID.Should().Be(999);
    }

    private static FosterFamilyRequest BuildValidRequest()
    {
        return new FosterFamilyRequest
        {
            SubmissionDate = DateTime.UtcNow,

            FosterCarer = new FosterCarerRequest
            {
                CarerFirstName = "Joe",
                CarerLastName = "Bloggs",
                CarerDateOfBirth = new DateTime(1980, 1, 1),
                CarerNationalInsuranceNumber = "AB123456C"
            },

            FosterChild = new FosterChildRequest
            {
                ChildFirstName = "Child",
                ChildLastName = "One",
                ChildDateOfBirth = new DateTime(2022, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };
    }
}