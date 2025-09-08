using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class SearchEstablishmentsUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockEstablishmentSearchGateway = new Mock<IEstablishmentSearch>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new SearchEstablishmentsUseCase(_mockEstablishmentSearchGateway.Object, _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockEstablishmentSearchGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IEstablishmentSearch> _mockEstablishmentSearchGateway;
    private Mock<IAudit> _mockAuditGateway;
    private SearchEstablishmentsUseCase _sut;

    [Test]
    public async Task Execute_Should_Return_Results_When_Successful()
    {
        // Arrange
        var query = "test";
        string la = null;
        var establishments = _fixture.CreateMany<Establishment>().ToList();
        _mockEstablishmentSearchGateway.Setup(es => es.Search(query, la)).ReturnsAsync(establishments);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Establishment, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        var result = await _sut.Execute(query, la);

        // Assert
        result.Should().BeEquivalentTo(establishments);
    }

    [Test]
    public async Task Execute_Should_Return_Empty_When_No_Results()
    {
        // Arrange
        var query = "test";
        string la = null;
        var establishments = new List<Establishment>();

        _mockEstablishmentSearchGateway.Setup(es => es.Search(query, la)).ReturnsAsync(establishments);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Establishment, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());


        // Act
        var result = await _sut.Execute(query, la);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public void Execute_Should_Throw_Exception_When_Query_Is_NullOrWhiteSpace()
    {
        // Arrange
        string query = null;
        string la = null;
        // Act
        Func<Task> act = async () => await _sut.Execute(query, la);

        // Assert
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Required input query was empty. (Parameter 'query')");
    }

    [Test]
    public void Execute_Should_Throw_Exception_When_Query_Length_Is_Less_Than_Three()
    {
        // Arrange
        var query = "ab";
        string la = null;

        // Act
        Func<Task> act = async () => await _sut.Execute(query, la);

        // Assert
        act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("query.Length must be between 3 and 2147483647 (Parameter 'query.Length')");
    }
}