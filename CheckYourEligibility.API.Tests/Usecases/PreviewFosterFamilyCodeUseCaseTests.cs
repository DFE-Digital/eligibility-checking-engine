using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class PreviewFosterFamilyCodeUseCaseTests
{
    private PreviewFosterFamilyCodeUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _sut = new PreviewFosterFamilyCodeUseCase();
    }

    [Test]
    public async Task Execute_Should_Throw_When_Request_Is_Null()
    {
        // Act
        Func<Task> act = () => _sut.Execute(null!, 201);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_Request_Is_Invalid()
    {
        // Arrange
        var request = new FosterFamilyRequest
        {
            SubmissionDate = DateTime.UtcNow,
            FosterCarer = new FosterCarerRequest
            {
                CarerFirstName = string.Empty,
                CarerLastName = "Bloggs",
                CarerDateOfBirth = new DateTime(1980, 1, 1),
                CarerNationalInsuranceNumber = "BAD"
            },
            FosterChild = new FosterChildRequest
            {
                ChildFirstName = "Child",
                ChildLastName = "One",
                ChildDateOfBirth = new DateTime(2022, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        // Act
        Func<Task> act = () => _sut.Execute(request, 201);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Return_Preview_Values_And_Assign_Local_Authority_Id()
    {
        // Arrange
        var request = BuildValidRequest();

        // Act
        var result = await _sut.Execute(request, 201);

        // Assert
        result.Should().NotBeNull();
        result.ValidityStartDate.Should().NotBe(default);
        result.ValidFromTerm.Should().NotBeNull();
        result.ValidFromTerm.Name.Should().NotBe(TermName.None);
        result.ReconfirmBetweenStart.Should().NotBe(default);
        result.ReconfirmBetweenEnd.Should().BeAfter(result.ReconfirmBetweenStart);
        result.GracePeriodEndDate.Should().BeAfter(result.ValidityStartDate);
        request.FosterCarer.LocalAuthorityID.Should().Be(201);
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
