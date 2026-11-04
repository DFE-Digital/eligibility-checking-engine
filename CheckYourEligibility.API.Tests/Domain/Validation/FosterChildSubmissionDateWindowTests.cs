using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants.ErrorMessages;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class FosterChildSubmissionDateWindowTests
{
    private static FosterChildRequest CreateRequest(int submittedDaysAgo) => new()
    {
        ChildFirstName = "Sam",
        ChildLastName = "Smith",
        ChildDateOfBirth = DateTime.Today.AddYears(-2),
        ChildPostCode = "AB1 2CD",
        SubmissionDate = DateTime.Today.AddDays(-submittedDaysAgo)
    };

    private static IConfiguration Config(string? value) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            value == null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["FosterFamilies:EnforceSubmissionDateWindow"] = value }).Build();

    [Test]
    public void Validator_DefaultsToEnforcing()
    {
        var result = new FosterChildRequestValidator().Validate(CreateRequest(32));

        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessages.SubmissionDateOutOfRange);
    }

    [Test]
    public void Validator_Enforced_AcceptsDateWithinWindow()
    {
        new FosterChildRequestValidator(true).Validate(CreateRequest(31)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validator_NotEnforced_AcceptsOldDate()
    {
        new FosterChildRequestValidator(false).Validate(CreateRequest(100)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validator_NotEnforced_StillRejectsFutureDate()
    {
        var result = new FosterChildRequestValidator(false).Validate(CreateRequest(-1));

        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessages.SubmissionDate);
    }

    [TestCase(null)]
    [TestCase("true")]
    public async Task CreateFosterChildUseCase_EnforcedWhenSettingMissingOrTrue_RejectsOldDate(string? setting)
    {
        var sut = new CreateFosterChildUseCase(new Mock<IFosterFamilies>(MockBehavior.Strict).Object, Config(setting));

        var act = () => sut.Execute(CreateRequest(32), 1, Guid.NewGuid(), DateTime.UtcNow);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .Which.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessages.SubmissionDateOutOfRange);
    }

    [Test]
    public async Task CreateFosterChildUseCase_SettingFalse_AcceptsOldDate()
    {
        var gateway = new Mock<IFosterFamilies>();
        gateway.Setup(x => x.CreateFosterChild(It.IsAny<FosterChildRequest>(), 1, It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new FosterChildResponse());
        var sut = new CreateFosterChildUseCase(gateway.Object, Config("false"));

        await sut.Execute(CreateRequest(32), 1, Guid.NewGuid(), DateTime.UtcNow);

        gateway.Verify(x => x.CreateFosterChild(It.IsAny<FosterChildRequest>(), 1, It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Once);
    }
}
