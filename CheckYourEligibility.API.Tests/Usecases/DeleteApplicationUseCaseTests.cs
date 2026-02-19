using AutoFixture;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class DeleteApplicationUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new DeleteApplicationUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
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
    private DeleteApplicationUseCase _sut = null!;
    private Fixture _fixture = null!;

    [Test]
    public async Task Execute_ValidGuidWithAllPermissions_DeletesApplicationSuccessfully()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 }; // 0 means "all" permissions
        var applicationLocalAuthorityId = _fixture.Create<int>();

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }

    [Test]
    public async Task Execute_ValidGuidWithMatchingLocalAuthority_DeletesApplicationSuccessfully()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var applicationLocalAuthorityId = 123;
        var localAuthorityIds = new List<int> { 123, 456 }; // Contains matching authority

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }

    [Test]
    public void Execute_ValidGuidWithNonMatchingLocalAuthority_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var applicationLocalAuthorityId = 999;
        var localAuthorityIds = new List<int> { 123, 456 }; // Does not contain 999

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);

        // Act & Assert
        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("You do not have permission to delete applications for this establishment's local authority");
    }

    [Test]
    public void Execute_ApplicationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 }; // "all" permissions
        var applicationLocalAuthorityId = _fixture.Create<int>();

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(false); // Application not found

        // Act & Assert
        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be($"Application with ID {guid} not found");
    }

    [Test]
    public void Execute_EmptyLocalAuthorityList_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var applicationLocalAuthorityId = 123;
        var localAuthorityIds = new List<int>(); // Empty list

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);

        // Act & Assert
        var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("You do not have permission to delete applications for this establishment's local authority");
    }

    [Test]
    public void Execute_NullLocalAuthorityList_ThrowsArgumentNullException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        List<int> localAuthorityIds = null!;

        // No mock setup needed since ArgumentNullException should be thrown immediately

        // Act & Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
    }

    [Test]
    public void Execute_ApplicationGatewayThrowsException_PropagatesException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 };
        var expectedException = new Exception("Database error");

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
        exception.Should().BeSameAs(expectedException);
    }

    [Test]
    public void Execute_AuditGatewayThrowsException_PropagatesException()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 };
        var applicationLocalAuthorityId = _fixture.Create<int>();
        var expectedException = new Exception("Audit service error");

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(
            async () => await _sut.Execute(guid, localAuthorityIds));

        exception.Should().NotBeNull();
        exception.Should().BeSameAs(expectedException);
    }

    [Test]
    public async Task Execute_ValidGuidWithMultipleLocalAuthorities_DeletesWhenOneMatches()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var applicationLocalAuthorityId = 456;
        var localAuthorityIds = new List<int> { 123, 456, 789 }; // 456 matches

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }

    [Test]
    public async Task Execute_ValidExecution_CreatesCorrectAuditEntry()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 };
        var applicationLocalAuthorityId = _fixture.Create<int>();
        var expectedAuditId = _fixture.Create<string>();

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(expectedAuditId);

        // Act
        await _sut.Execute(guid, localAuthorityIds);

        // Assert
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(AuditType.Application, guid,null), Times.Once);
    }

    [Test]
    public async Task Execute_CallsGatewayMethodsInCorrectOrder()
    {
        // Arrange
        var guid = _fixture.Create<string>();
        var localAuthorityIds = new List<int> { 0 };
        var applicationLocalAuthorityId = _fixture.Create<int>();
        var sequence = new MockSequence();

        _mockApplicationGateway.InSequence(sequence)
            .Setup(x => x.GetLocalAuthorityIdForApplication(guid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.InSequence(sequence)
            .Setup(x => x.DeleteApplication(guid))
            .ReturnsAsync(true);
        _mockAuditGateway.InSequence(sequence)
            .Setup(x => x.CreateAuditEntry(AuditType.Application, guid,null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(guid, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
        // The sequence ensures methods are called in the correct order
    }

    [Test]
    public async Task Execute_WithStringGuid_HandlesCorrectly()
    {
        // Arrange
        var stringGuid = Guid.NewGuid().ToString();
        var localAuthorityIds = new List<int> { 0 };
        var applicationLocalAuthorityId = _fixture.Create<int>();

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdForApplication(stringGuid))
            .ReturnsAsync(applicationLocalAuthorityId);
        _mockApplicationGateway.Setup(x => x.DeleteApplication(stringGuid))
            .ReturnsAsync(true);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Application, stringGuid, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(stringGuid, localAuthorityIds);

        // Assert
        // Verification is handled in TearDown through VerifyAll()
    }
}