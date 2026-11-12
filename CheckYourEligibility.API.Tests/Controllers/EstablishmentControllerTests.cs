using AutoFixture;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.Controllers;

public class EstablishmentControllerTests : ControllerTestBase
{
    private Mock<IAudit> _mockAuditGateway;
    private Mock<IApplication> _mockApplicationGateway;
    private ILogger<EstablishmentController> _mockLogger;
    private Mock<ISearchEstablishmentsUseCase> _mockSearchUseCase;
    private Mock<ISupportSearchEstablishmentUseCase> _mockSupportSearchUseCase;
    private Mock<ISupportGetEstablishmentUseCase> _mockSupportGetEstablishmentUseCase;
    private Mock<IUpdateEstablishmentPrivateBetaUseCase> _mockUpdateEstablishmentPrivateBetaUseCase;
    private EstablishmentController _sut;

    [SetUp]
    public void Setup()
    {
        _mockSearchUseCase = new Mock<ISearchEstablishmentsUseCase>(MockBehavior.Strict);

        _mockSupportSearchUseCase = new Mock<ISupportSearchEstablishmentUseCase>(MockBehavior.Strict);

        _mockSupportGetEstablishmentUseCase = new Mock<ISupportGetEstablishmentUseCase>(MockBehavior.Strict);

        _mockUpdateEstablishmentPrivateBetaUseCase = new Mock<IUpdateEstablishmentPrivateBetaUseCase>(MockBehavior.Strict);

        _mockLogger = Mock.Of<ILogger<EstablishmentController>>();

        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);

        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);

        _sut = new EstablishmentController(
            _mockLogger,
            _mockSearchUseCase.Object,
            _mockSupportSearchUseCase.Object,
            _mockSupportGetEstablishmentUseCase.Object,
            _mockUpdateEstablishmentPrivateBetaUseCase.Object,
            _mockAuditGateway.Object,
            _mockApplicationGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockSearchUseCase.VerifyAll();
        _mockSupportSearchUseCase.VerifyAll();
        _mockSupportGetEstablishmentUseCase.VerifyAll();
        _mockUpdateEstablishmentPrivateBetaUseCase.VerifyAll();
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

    [Test]
    public async Task Given_Support_Search_Should_Return_Status200OK()
    {
        // Arrange
        var request = _fixture.Create<SupportEstablishmentSearchRequest>();
        var result = _fixture.Create<SupportEstablishmentSearchResponse>();

        _mockSupportSearchUseCase.Setup(x => x.Execute(request)).ReturnsAsync(result);

        // Act
        var response = await _sut.Support_Search(request);

        // Assert
        response.Should().BeOfType<OkObjectResult>();

        var okResult = response as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(result);
    }

    [Test]
    public async Task Given_GetEstablishment_With_Invalid_Id_Should_Return_Status400BadRequest()
    {
        // Arrange
        var establishmentId = 0;

        // Act
        var response = await _sut.GetEstablishment(establishmentId);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Given_GetEstablishment_When_NotFound_Should_Return_Status404NotFound()
    {
        // Arrange
        var establishmentId = _fixture.Create<int>();

        _mockSupportGetEstablishmentUseCase.Setup(x => x.Execute(establishmentId)).ReturnsAsync((SupportEstablishmentResponse?)null);

        // Act
        var response = await _sut.GetEstablishment(establishmentId);

        // Assert
        response.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task Given_GetEstablishment_Should_Return_Status200OK()
    {
        // Arrange
        var establishmentId = _fixture.Create<int>();
        var result = _fixture.Create<SupportEstablishmentResponse>();

        _mockSupportGetEstablishmentUseCase.Setup(x => x.Execute(establishmentId)).ReturnsAsync(result);

        // Act
        var response = await _sut.GetEstablishment(establishmentId);

        // Assert
        response.Should().BeOfType<OkObjectResult>();

        var okResult = response as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(result);
    }

    [Test]
    public async Task Given_UpdatePrivateBeta_When_NotFound_Should_Return_Status404NotFound()
    {
        // Arrange
        var request = _fixture.Create<UpdateEstablishmentPrivateBetaRequest>();

        _mockUpdateEstablishmentPrivateBetaUseCase.Setup(x => x.Execute(request.EstablishmentId,request.InPrivateBeta)).ReturnsAsync(false);

        // Act
        var response = await _sut.UpdatePrivateBeta(request);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task Given_UpdatePrivateBeta_Should_Return_Status200OK()
    {
        // Arrange
        var request = _fixture.Create<UpdateEstablishmentPrivateBetaRequest>();

        _mockUpdateEstablishmentPrivateBetaUseCase.Setup(x => x.Execute(request.EstablishmentId,request.InPrivateBeta)).ReturnsAsync(true);

        // Act
        var response = await _sut.UpdatePrivateBeta(request);

        // Assert
        response.Should().BeOfType<OkResult>();
    }
}