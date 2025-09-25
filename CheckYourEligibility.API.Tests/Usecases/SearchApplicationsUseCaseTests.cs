using System.Threading.Tasks;
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
public class SearchApplicationsUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new SearchApplicationsUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
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
    private SearchApplicationsUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_Should_Throw_ArgumentException_When_Model_Is_Null()
    {
        // Arrange
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(null!, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Invalid request, data is required");
    }

    [Test]
    public async Task Execute_Should_Throw_ArgumentException_When_Model_Data_Is_Null()
    {
        // Arrange
        var model = new ApplicationRequestSearch { Data = null };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Invalid request, data is required");
    }

    [Test]
    public async Task Execute_Should_Throw_ArgumentException_When_Neither_LocalAuthority_Nor_Establishment_Nor_MultiAcademyTrust_Are_Provided()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = null,
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<ArgumentException>();

        exception.WithMessage("Either LocalAuthority, Establishment, or MultiAcademyTrust must be specified");
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_MultiAcademyTrust_Not_Allowed()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 999, // Not in allowed list
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { };
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 }; // Specific authorities only, not including 0 (all)

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage("You do not have permission to search applications for this multi academy trust");
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_LocalAuthority_Not_Allowed()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = 999, // Not in allowed list
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 }; // Specific authorities only, not including 0 (all)
        var allowedMultiAcademyTrustIds = new List<int> { };

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage("You do not have permission to search applications for this local authority");
    }

    [Test]
    public async Task Execute_Should_Allow_LocalAuthority_When_User_Has_All_Permissions()
    {
        // Arrange
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = 999, // Any local authority should be allowed
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 0 }; // 0 means 'all' permissions
        var allowedMultiAcademyTrustIds = new List<int> { };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Throw_UnauthorizedAccessException_When_Establishment_LocalAuthority_MAT_Not_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var localAuthorityIdFromEstablishment = 999; // Not in allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = null,
                Establishment = establishmentId
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 }; // Specific authorities only
        var allowedMultiAcademyTrustIds = new List<int> { };

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(establishmentId))
            .ReturnsAsync(localAuthorityIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ThrowsAsync(new Exception($"Unable to find school:- {establishmentId} in MAT data"));

        // Act & Assert
        var exception = await FluentActions.Invoking(() => _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        exception.WithMessage(
            "You do not have permission to search applications for this establishment's local authority or multi academy trust");
    }

    [Test]
    public async Task Execute_Should_Allow_Establishment_When_LocalAuthority_Is_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var localAuthorityIdFromEstablishment = 2; // In allowed list
        var multiAcademyTrustId = 2; // Not in allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = null,
                Establishment = establishmentId
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(establishmentId))
            .ReturnsAsync(localAuthorityIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(multiAcademyTrustId);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Establishment_When_MultiAcademyTrust_Is_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var localAuthorityId = 2; // Not in allowed list
        var multiAcademyTrustIdFromEstablishment = 2; // In allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = null,
                Establishment = establishmentId
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1 };
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(multiAcademyTrustIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(establishmentId))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Both_LocalAuthority_And_Establishment_When_Both_Are_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var localAuthorityIdFromEstablishment = 2; // In allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                LocalAuthority = 1, // Also in allowed list
                Establishment = establishmentId
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(establishmentId))
            .ReturnsAsync(localAuthorityIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetLocalAuthorityIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId), Times.Once);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Allow_Both_MultiAcademyTrust_And_Establishment_When_Both_Are_Allowed()
    {
        // Arrange
        var establishmentId = 123;
        var localAuthorityId = 2; // Not inn allowed list
        var multiAcademyTrustIdFromEstablishment = 2; // In allowed list
        var model = _fixture.Build<ApplicationRequestSearch>()
            .With(x => x.Data, new ApplicationRequestSearchData
            {
                MultiAcademyTrust = 1, // Also in allowed list
                Establishment = establishmentId
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { };
        var allowedMultiAcademyTrustIds = new List<int> { 1, 2, 3 };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetLocalAuthorityIdForEstablishment(establishmentId))
            .ReturnsAsync(localAuthorityId);
        _mockApplicationGateway.Setup(s => s.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(multiAcademyTrustIdFromEstablishment);
        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

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
                LocalAuthority = 1,
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync((ApplicationSearchResponse?)null);

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

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
                LocalAuthority = 1,
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };
        var response = _fixture.Build<ApplicationSearchResponse>()
            .With(r => r.Data, new List<ApplicationResponse>())
            .Create();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

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
                LocalAuthority = 1,
                Establishment = null
            })
            .Create();
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };
        var allowedMultiAcademyTrustIds = new List<int> { };
        var response = _fixture.Create<ApplicationSearchResponse>();

        _mockApplicationGateway.Setup(s => s.GetApplications(model)).ReturnsAsync(response);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(model, allowedLocalAuthorityIds, allowedMultiAcademyTrustIds);

        // Assert
        result.Should().Be(response);
        _mockApplicationGateway.Verify(s => s.GetApplications(model), Times.Once);
        _mockAuditGateway.Verify(a => a.CreateAuditEntry(AuditType.Administration, string.Empty), Times.Once);
    }
}