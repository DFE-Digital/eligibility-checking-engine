using AutoFixture;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateFosterFamilyUseCaseTests
{
     private Mock<IFosterFamily> _mockFosterFamilyGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private CreateFosterFamilyUseCase _sut = null!;
    private Fixture _fixture = null!;
    private List<int> _allowedLocalAuthorityIds = null!;
    private FosterFamilyRequest _validFosterFamilyRequest = null!;

    [SetUp]
    public void Setup()
    {
        _mockFosterFamilyGateway = new Mock<IFosterFamily>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new CreateFosterFamilyUseCase(_mockFosterFamilyGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();

        _allowedLocalAuthorityIds = new List<int> { 0, 1, 2, 3 };

        _validFosterFamilyRequest = new FosterFamilyRequest
        {
            Data = new FosterFamilyRequestData
            {
                CarerFirstName = "John",
                CarerLastName = "Doe",
                CarerDateOfBirth = new DateOnly(1980, 5, 15), // autofixture does not support DateOnly great
                CarerNationalInsuranceNumber = "AB123456C",
                HasPartner = false,
                PartnerFirstName = null,
                PartnerLastName = null,
                PartnerDateOfBirth = null,
                PartnerNationalInsuranceNumber = null,
                ChildFirstName = "Emily",
                ChildLastName = "Doe",
                ChildDateOfBirth = new DateOnly(2015, 3, 10),
                ChildPostCode = "SW1A 1AA",
                SubmissionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            }
            
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
        Func<Task> act = async () => await _sut.Execute(null!, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_ModelData_Is_Null()
    {
        // Arrange
        var model = new FosterFamilyRequest { Data = null! };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

   

}