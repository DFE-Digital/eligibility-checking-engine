using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateFosterFamilyUseCaseTests
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
    public async Task Execute_Should_Throw_When_Request_Is_Null()
    {
        // Arrange

        // Act
        var act = () => _sut.Execute(
            null!,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_User_Does_Not_Have_LA_Access()
    {
        // Arrange
        var request = BuildValidRequest();

        _mockGateway
            .Setup(x => x.CreateFosterFamily(request))
            .ThrowsAsync(new UnauthorizedAccessException("User does not have access to this local authority"));

        // Act
        var act = () => _sut.Execute(
            request,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Execute_Should_Throw_ValidationException_When_Request_Is_Invalid()
    {
        // Arrange
        var request = new FosterFamilyRequest
        {
            FosterCarer = new FosterCarerRequest(),
            FosterChild = new FosterChildRequest()
        };

        // Act
        var act = () => _sut.Execute(
            request,
            1);

        // Assert
        await FluentActions
            .Invoking(act)
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
            FosterCarerId = Guid.NewGuid(),
            FosterChildId = Guid.NewGuid()
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
            FosterCarerId = Guid.NewGuid(),
            FosterChildId = Guid.NewGuid()
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

    [TestCase("ab 12 34 56 c", "ce 12 34 56 a")]
    [TestCase("ab-12.34/56c", "ce-12.34/56a")]
    [TestCase("ab\t12\r\n3456c", "ce\t12\r\n3456a")]
    public async Task Execute_ForwardsCanonicalCarerAndPartnerNinos(
    string carerNino,
    string partnerNino)
    {
        var request = BuildValidRequest();
        request.FosterCarer.CarerNationalInsuranceNumber = carerNino;
        request.HasPartner = true;
        request.Partner = new FosterPartnerRequest
        {
            PartnerFirstName = "Jane",
            PartnerLastName = "Bloggs",
            PartnerDateOfBirth = new DateTime(1981, 1, 1),
            PartnerNationalInsuranceNumber = partnerNino
        };

        _mockGateway
            .Setup(x => x.CreateFosterFamily(
                It.Is<FosterFamilyRequest>(r =>
                    r.FosterCarer.CarerNationalInsuranceNumber == "AB123456C" &&
                    r.Partner != null &&
                    r.Partner.PartnerNationalInsuranceNumber == "CE123456A")))
            .ReturnsAsync(new FosterFamilyCreatedResponse());

        await _sut.Execute(request, 1);
    }

    [Test]
    public async Task Execute_RejectsInvalidNino_WithoutCallingGateway(
    [Values(false, true)] bool invalidPartner,
    [Values(
        "BG123456C",
        "A1123456C",
        "AB123456E",
        "AB123456 ",
        "AB12345C",
        "AB123456CD",
        "AB\u0661\u0662\u0663\u0664\u0665\u0666C",
        "---")] string invalidNino)
    {
        var request = BuildValidRequest();
        request.HasPartner = true;
        request.Partner = new FosterPartnerRequest
        {
            PartnerFirstName = "Jane",
            PartnerLastName = "Bloggs",
            PartnerDateOfBirth = new DateTime(1981, 1, 1),
            PartnerNationalInsuranceNumber = "CE123456A"
        };

        if (invalidPartner)
        {
            request.Partner.PartnerNationalInsuranceNumber = invalidNino;
        }
        else
        {
            request.FosterCarer.CarerNationalInsuranceNumber = invalidNino;
        }

        var expectedProperty = invalidPartner
            ? "Partner.PartnerNationalInsuranceNumber"
            : "FosterCarer.CarerNationalInsuranceNumber";

        Func<Task> act = () => _sut.Execute(request, 1);

        var thrown = await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>();

        thrown.Which.Errors.Should().NotBeEmpty();
        thrown.Which.Errors.Should().OnlyContain(
            error => error.PropertyName == expectedProperty);

        var retainedNino = invalidPartner
            ? request.Partner!.PartnerNationalInsuranceNumber
            : request.FosterCarer.CarerNationalInsuranceNumber;

        retainedNino.Should().Be(invalidNino);

        _mockGateway.Verify(
            gateway => gateway.CreateFosterFamily(
                It.IsAny<FosterFamilyRequest>()),
            Times.Never);
    }

    [Test]
    public async Task Execute_Should_Preserve_Unvalidated_Partner_When_HasPartner_Is_False()
    {
        var request = BuildValidRequest();
        request.HasPartner = false;
        request.FosterCarer.CarerNationalInsuranceNumber = "ab-12.34/56c";

        var partner = new FosterPartnerRequest
        {
            PartnerFirstName = string.Empty,
            PartnerLastName = string.Empty,
            PartnerDateOfBirth = default,
            PartnerNationalInsuranceNumber = "bg-12.34/56c"
        };
        request.Partner = partner;

        _mockGateway
            .Setup(gateway => gateway.CreateFosterFamily(
                It.Is<FosterFamilyRequest>(r =>
                    !r.HasPartner &&
                    r.FosterCarer.CarerNationalInsuranceNumber == "AB123456C" &&
                    r.Partner == partner &&
                    r.Partner.PartnerNationalInsuranceNumber == "bg-12.34/56c")))
            .ReturnsAsync(new FosterFamilyCreatedResponse());

        await _sut.Execute(request, 1);

        request.Partner.Should().BeSameAs(partner);
        partner.PartnerNationalInsuranceNumber.Should().Be("bg-12.34/56c");
        partner.PartnerFirstName.Should().BeEmpty();
        partner.PartnerLastName.Should().BeEmpty();
        partner.PartnerDateOfBirth.Should().Be(default(DateTime));

        _mockGateway.Verify(
            gateway => gateway.CreateFosterFamily(request),
            Times.Once);
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