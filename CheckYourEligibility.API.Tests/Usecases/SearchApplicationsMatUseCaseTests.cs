using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class SearchApplicationsMatUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new SearchApplicationsMatUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockApplicationGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private SearchApplicationsMatUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public void Execute_Should_Throw_ArgumentException_When_Model_Is_Null()
    {
        // Arrange
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };

        // Act & Assert
        var exception = FluentActions.Invoking(() => _sut.Execute(null!, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Invalid request, data is required");
    }

    [Test]
    public void Execute_Should_Throw_ArgumentException_When_Model_Data_Is_Null()
    {
        // Arrange
        var model = new ApplicationRequestSearch { Data = null };
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };

        // Act & Assert
        var exception = FluentActions.Invoking(() => _sut.Execute(model, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Invalid request, data is required");
    }

    [Test]
    public void Execute_Should_Throw_ArgumentException_When_Neither_MultiAcademyTrust_Nor_Establishment_Are_Provided()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = null,
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };

        // Act & Assert
        var exception = FluentActions.Invoking(() => _sut.Execute(model, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Either MultiAcademyTrust or Establishment must be specified");
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_MultiAcademyTrust_Not_Allowed()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 999, // Not in allowed list
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 }; // Specific MATs only, not including 0 (all)

        // Act & Assert
        var exception = FluentActions.Invoking(() => _sut.Execute(model, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage("You do not have permission to search applications for this multi academy trust");
    }

    [Test]
    public async Task Execute_Should_Allow_MultiAcademyTrust_When_User_Has_All_Permissions()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 999, // Any multi academy trust should be allowed
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 0 }; // 0 means 'all' permissions
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public void Execute_Should_Throw_UnauthorizedAccessException_When_Establishment_MultiAcademyTrust_Not_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var MultiAcademyTrustIdFromEstablishment = 999; // Not in allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = null,
                Establishment = establishmentId
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 }; // Specific authorities only

        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(MultiAcademyTrustIdFromEstablishment);

        // Act & Assert
        var exception = FluentActions.Invoking(() => _sut.Execute(model, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage(
            "You do not have permission to search applications for this establishment's multi academy trust");
    }

    [Test]
    public async Task Execute_Should_Allow_Establishment_When_MultiAcademyTrust_Is_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var MultiAcademyTrustIdFromEstablishment = 2; // In allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = null,
                Establishment = establishmentId
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(MultiAcademyTrustIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Both_MultiAcademyTrust_And_Establishment_When_Both_Are_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var MultiAcademyTrustIdFromEstablishment = 2; // In allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 1, // Also in allowed list
                Establishment = establishmentId
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(MultiAcademyTrustIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Return_Empty_Response_When_Gateway_Returns_Null()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 1,
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync((ApplicationSearchResponse?)null);

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task Execute_Should_Return_Empty_Response_When_Gateway_Returns_Empty_Data()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 1,
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Build<ApplicationSearchResponse>()
            .With(r => r.Data, new List<ApplicationResponse>())
            .Create();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task Execute_Should_Return_Response_When_Gateway_Returns_Valid_Data()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 1,
                Establishment = null
            })
            .Create();
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Administration, string.Empty), Times.Once);
    }
}