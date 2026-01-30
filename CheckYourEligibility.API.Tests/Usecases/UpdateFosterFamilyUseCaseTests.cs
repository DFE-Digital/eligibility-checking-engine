using AutoFixture;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateFosterFamilyUseCaseTests
{
    private Mock<IFosterFamily> _mockFosterFamilyGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private UpdateFosterFamilyUseCase _sut = null!;
    private List<int> _allowedLocalAuthorityIds = null!;
    private FosterFamilyUpdateRequest _validFosterFamilyUpdateRequest = null!;

    [SetUp]
    public void Setup()
    {
        _mockFosterFamilyGateway = new Mock<IFosterFamily>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new UpdateFosterFamilyUseCase(_mockFosterFamilyGateway.Object, _mockAuditGateway.Object);

        _allowedLocalAuthorityIds = new List<int> { 0, 1, 2, 3 };

        _validFosterFamilyUpdateRequest = new FosterFamilyUpdateRequest
        {

            CarerFirstName = "John",
            CarerLastName = "Doe",
            CarerDateOfBirth = new DateTime(1980, 5, 15),
            CarerNationalInsuranceNumber = "AB123456C",
            HasPartner = false,
            PartnerFirstName = null,
            PartnerLastName = null,
            PartnerDateOfBirth = null,
            PartnerNationalInsuranceNumber = null,
            ChildFirstName = "Emily",
            ChildLastName = "Doe",
            ChildDateOfBirth = new DateTime(2015, 3, 10),
            ChildPostCode = "SW1A 1AA",

        };

    }

    [TearDown]
    public void Teardown()
    {
        _mockFosterFamilyGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_Model_Is_Null()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute("123546", null!);

        // Assert
        act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_ModelData_Is_Null()
    {
        // Arrange
        var model = new FosterFamilyRequest { Data = null! };

        // Act
        Func<Task> act = async () => await _sut.Execute("123456", null!);

        // Assert
        act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }


















}