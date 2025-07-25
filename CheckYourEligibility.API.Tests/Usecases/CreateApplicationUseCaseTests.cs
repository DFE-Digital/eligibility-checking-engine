using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateApplicationUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new CreateApplicationUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();

        // Default allowed local authorities - includes "all" authority (0)
        _allowedLocalAuthorityIds = new List<int> { 0, 1, 2, 3 };

        _validApplicationRequest = _fixture.Build<ApplicationRequest>()
            .With(x => x.Data, _fixture.Build<ApplicationRequestData>()
                .With(d => d.Type, CheckEligibilityType.FreeSchoolMeals)
                .With(d => d.ParentNationalInsuranceNumber, "ns738356d")
                .With(d => d.ParentDateOfBirth, "1970-02-01")
                .With(d => d.ChildDateOfBirth, "1970-02-01")
                .With(d => d.ParentNationalAsylumSeekerServiceNumber, string.Empty)
                .With(d => d.Establishment, 12345) // Establishment is an int
                .Create())
            .Create();
    }

    [TearDown]
    public void Teardown()
    {
        _mockApplicationGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private CreateApplicationUseCase _sut = null!;
    private List<int> _allowedLocalAuthorityIds = null!;
    private Fixture _fixture = null!;

    // valid application request
    private ApplicationRequest _validApplicationRequest = null!;

    [Test]
    public void Execute_Should_Throw_ValidationException_When_Model_Is_Null()
    {
        // Act
        Func<Task> act = async () => await _sut.Execute(null!, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid request, data is required");
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_ModelData_Is_Null()
    {
        // Arrange
        var model = new ApplicationRequest { Data = null };

        // Act
        Func<Task> act = async () => await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid request, data is required");
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_ModelData_Type_Is_None()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequest>()
            .With(x => x.Data, _fixture.Build<ApplicationRequestData>()
                .With(d => d.Type, CheckEligibilityType.None)
                .Create())
            .Create();

        // Act
        Func<Task> act = async () => await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid request, Valid Type is required: None");
    }

    [Test]
    public void Execute_Should_Throw_ValidationException_When_ApplicationRequestValidator_Fails()
    {
        // Arrange
        // Create an application with invalid data that will trigger the ApplicationRequestValidator
        var model = _fixture.Build<ApplicationRequest>()
            .With(x => x.Data, _fixture.Build<ApplicationRequestData>()
                .With(d => d.Type, CheckEligibilityType.FreeSchoolMeals)
                .With(d => d.ParentNationalInsuranceNumber, "invalid-format") // Invalid NI number format
                .Create())
            .Create();

        // Act
        Func<Task> act = async () => await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Execute_Should_Call_PostApplication_On_ApplicationGateway()
    {
        // Arrange
        var model = _validApplicationRequest;
        var response = _fixture.Create<ApplicationResponse>();
        var localAuthorityId = 1; // This matches an allowed authority

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.PostApplication(model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, response.Id))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment),
            Times.Once);
        _mockApplicationGateway.Verify(s => s.PostApplication(model.Data!), Times.Once);
        result.Data.Should().Be(response);
    }

    [Test]
    public async Task Execute_Should_Succeed_When_AllLocalAuthorities_Are_Allowed()
    {
        // Arrange
        var model = _validApplicationRequest;
        var response = _fixture.Create<ApplicationResponse>();
        var localAuthorityId = 999; // This doesn't match any specific allowed authority
        var allAuthoritiesAllowed = new List<int> { 0 }; // 0 means all authorities are allowed

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.PostApplication(model.Data!)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Application, response.Id))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allAuthoritiesAllowed);

        // Assert
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment),
            Times.Once);
        _mockApplicationGateway.Verify(s => s.PostApplication(model.Data!), Times.Once);
        result.Data.Should().Be(response);
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthority_Not_Allowed()
    {
        // Arrange
        var model = _validApplicationRequest;
        var localAuthorityId = 999; // This doesn't match any allowed authority
        var restrictedAuthorities = new List<int> { 1, 2, 3 }; // Specific authorities only, not including 0 (all)

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment))
            .ReturnsAsync(localAuthorityId);

        // Act
        Func<Task> act = async () => await _sut.Execute(model, restrictedAuthorities);

        // Assert
        act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not have permission to create applications for this establishment's local authority");
    }

    [Test]
    public void Execute_Should_Throw_Exception_When_PostApplication_Returns_Null()
    {
        // Arrange
        var model = _validApplicationRequest;
        var localAuthorityId = 1; // This matches an allowed authority

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(model.Data!.Establishment))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.PostApplication(model.Data!)).ReturnsAsync((ApplicationResponse)null!);

        // Act
        Func<Task> act = async () => await _sut.Execute(model, _allowedLocalAuthorityIds);

        // Assert
        act.Should().ThrowAsync<Exception>().WithMessage("Failed to create application");
    }
}