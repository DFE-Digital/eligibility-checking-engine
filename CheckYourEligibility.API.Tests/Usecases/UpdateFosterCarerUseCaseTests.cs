using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateFosterCarerUseCaseTests
{
    private Mock<IFosterFamilies> _gateway = null!;
    private UpdateFosterCarerUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _gateway = new Mock<IFosterFamilies>(MockBehavior.Strict);
        _sut = new UpdateFosterCarerUseCase(_gateway.Object);
    }

    [Test]
    public async Task Execute_ForwardsCanonicalNino(
        [Values(false, true)] bool partner,
        [Values(
            "ab 12 34 56 c",
            "ab-12.34/56c",
            "ab\t12\r\n3456c")] string input)
    {
        var id = Guid.NewGuid();
        var request = BuildRequest(partner, input);

        _gateway
            .Setup(g => g.UpdateFosterCarer(
                id,
                201,
                It.Is<UpdateFosterCarerRequest>(r =>
                    partner
                        ? r.FosterPartnerRequest != null &&
                          r.FosterPartnerRequest
                              .PartnerNationalInsuranceNumber == "AB123456C"
                        : r.FosterCarerRequest != null &&
                          r.FosterCarerRequest
                              .CarerNationalInsuranceNumber == "AB123456C")))
            .Returns(Task.CompletedTask);

        await _sut.Execute(id, 201, request);

        _gateway.VerifyAll();
        _gateway.Verify(
            g => g.UpdateFosterCarer(id, 201, request),
            Times.Once);
    }

    [Test]
    public async Task Execute_RejectsInvalidNino_WithoutCallingGateway(
        [Values(false, true)] bool partner,
        [Values(
            "BG123456C",
            "A1123456C",
            "AB123456E",
            "AB123456 ",
            "AB12345C",
            "AB123456CD",
            "AB\u0661\u0662\u0663\u0664\u0665\u0666C",
            "---")] string input)
    {
        var request = BuildRequest(partner, input);
        var expectedProperty = partner
            ? "PartnerNationalInsuranceNumber"
            : "CarerNationalInsuranceNumber";

        Func<Task> act = () =>
            _sut.Execute(Guid.NewGuid(), 201, request);

        var thrown = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        thrown.Which.Errors.Should().NotBeEmpty();
        thrown.Which.Errors.Should().OnlyContain(
            error => error.PropertyName == expectedProperty);

        var retainedNino = partner
            ? request.FosterPartnerRequest!.PartnerNationalInsuranceNumber
            : request.FosterCarerRequest!.CarerNationalInsuranceNumber;

        retainedNino.Should().Be(input);

        _gateway.Verify(
            g => g.UpdateFosterCarer(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<UpdateFosterCarerRequest>()),
            Times.Never);
    }

    [Test]
    public async Task Execute_Should_Preserve_Both_Ninos_When_Partner_Is_Invalid()
    {
        const string carerNino = "ab-12.34/56c";
        const string partnerNino = "bg-12.34/56c";

        var request = BuildRequest(false, carerNino);
        request.FosterPartnerRequest =
            BuildRequest(true, partnerNino).FosterPartnerRequest;

        Func<Task> act = () =>
            _sut.Execute(Guid.NewGuid(), 201, request);

        var thrown = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        thrown.Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("PartnerNationalInsuranceNumber");

        request.FosterCarerRequest!.CarerNationalInsuranceNumber
            .Should().Be(carerNino);
        request.FosterPartnerRequest!.PartnerNationalInsuranceNumber
            .Should().Be(partnerNino);

        _gateway.Verify(
            g => g.UpdateFosterCarer(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<UpdateFosterCarerRequest>()),
            Times.Never);
    }

    private static UpdateFosterCarerRequest BuildRequest(
        bool partner,
        string nino)
    {
        return partner
            ? new UpdateFosterCarerRequest
            {
                FosterPartnerRequest = new FosterPartnerRequest
                {
                    PartnerFirstName = "Jane",
                    PartnerLastName = "Bloggs",
                    PartnerDateOfBirth = new DateTime(1981, 1, 1),
                    PartnerNationalInsuranceNumber = nino
                }
            }
            : new UpdateFosterCarerRequest
            {
                FosterCarerRequest = new FosterCarerRequest
                {
                    CarerFirstName = "Joe",
                    CarerLastName = "Bloggs",
                    CarerDateOfBirth = new DateTime(1980, 1, 1),
                    CarerNationalInsuranceNumber = nino
                }
            };
    }
}