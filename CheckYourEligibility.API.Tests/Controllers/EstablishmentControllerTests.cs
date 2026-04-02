using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class EstablishmentControllerTests : TestBase.TestBase
{
    private Fixture _fixture;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<IApplication> _mockApplicationGateway;
    private ILogger<EstablishmentController> _mockLogger;
    private Mock<ISearchEstablishmentsUseCase> _mockSearchUseCase;
    private EstablishmentController _sut;

    [SetUp]
    public void Setup()
    {
        _mockSearchUseCase = new Mock<ISearchEstablishmentsUseCase>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<EstablishmentController>>();
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _sut = new EstablishmentController(
            _mockLogger,
            _mockSearchUseCase.Object,
            _mockAuditGateway.Object,
            _mockApplicationGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockSearchUseCase.VerifyAll();
        _mockApplicationGateway.VerifyAll();
    }

    [Test]
    public async Task Given_Search_Should_Return_Status200OK()
    {
        // Arrange
        var query = _fixture.Create<string>();
        var result = _fixture.CreateMany<Establishment>().ToList();
        string la = null;
        string mat = null;
        _mockSearchUseCase.Setup(cs => cs.Execute(query, la, mat)).ReturnsAsync(result);

        var expectedResult = new ObjectResult(new EstablishmentSearchResponse { Data = result })
            { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.Search(query, la, mat);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_Search_Should_Return_Status400BadRequest()
    {
        // Arrange
        var query = "A";
        string la = null;
        string mat = null;

        // Act
        var response = await _sut.Search(query, la, mat);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Given_Search_Should_Return_Status200NotFound()
    {
        // Arrange
        var query = _fixture.Create<string>();
        var result = Enumerable.Empty<Establishment>();
        string la = null;
        string mat = null;
        _mockSearchUseCase.Setup(cs => cs.Execute(query, la, mat)).ReturnsAsync(result);

        var expectedResult = new ObjectResult(new EstablishmentSearchResponse { Data = result })
            { StatusCode = StatusCodes.Status200OK };

        // Act
        var response = await _sut.Search(query, la, mat);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_GetMultiAcademyTrustId_Should_Return_Status200OK()
    {
        // Arrange
        var establishmentId = _fixture.Create<int>();
        var matId = _fixture.Create<int>();

        _mockApplicationGateway
            .Setup(x => x.GetMultiAcademyTrustIdForEstablishment(establishmentId))
            .ReturnsAsync(matId);

        var expectedResult = new ObjectResult(matId)
        {
            StatusCode = StatusCodes.Status200OK
        };

        // Act
        var response = await _sut.GetMultiAcademyTrustIdForEstablishment(establishmentId);

        // Assert
        response.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Given_GetMultiAcademyTrustId_With_Invalid_Id_Should_Return_Status400BadRequest()
    {
        // Arrange
        var establishmentId = 0;

        // Act
        var response = await _sut.GetMultiAcademyTrustIdForEstablishment(establishmentId);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }
}