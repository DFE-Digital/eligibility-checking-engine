using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ValidationException = FluentValidation.ValidationException;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Exceptions;

namespace CheckYourEligibility.API.Tests.Controllers;

public class FosterFamilyControllerTests : TestBase
{
    private Mock<IGetFosterFamilyUseCase> _mockGetFosterFamily = null!;
    private Mock<ICreateFosterFamilyUseCase> _mockCreateFosterFamily = null!;
    private Mock<IUpdateFosterCarerUseCase> _mockUpdateFosterCarer = null!;
    private Mock<IDeleteFosterCarerUseCase> _mockDeleteFosterCarer = null!;
    private Mock<IDeleteFosterPartnerUseCase> _mockDeleteFosterPartner = null!;
    private Mock<ISearchFosterFamiliesUseCase> _mockSearchFosterFamilies = null!;
    private Mock<IGetFosterChildUseCase> _mockGetFosterChild = null!;
    private Mock<ICreateFosterChildUseCase> _mockCreateFosterChild = null!;
    private Mock<IUpdateFosterChildUseCase> _mockUpdateFosterChild = null!;
    private Mock<IDeleteFosterChildUseCase> _mockDeleteFosterChild = null!;
    private Mock<IAudit> _mockAudit = null!;

    private IConfigurationRoot _configuration = null!;
    private FosterFamilyController _sut = null!;

    [SetUp]
    public void Setup()
    {
        _mockGetFosterFamily = new Mock<IGetFosterFamilyUseCase>(MockBehavior.Strict);
        _mockCreateFosterFamily = new Mock<ICreateFosterFamilyUseCase>(MockBehavior.Strict);
        _mockUpdateFosterCarer = new Mock<IUpdateFosterCarerUseCase>(MockBehavior.Strict);
        _mockDeleteFosterCarer = new Mock<IDeleteFosterCarerUseCase>(MockBehavior.Strict);
        _mockDeleteFosterPartner = new Mock<IDeleteFosterPartnerUseCase>(MockBehavior.Strict);
        _mockSearchFosterFamilies = new Mock<ISearchFosterFamiliesUseCase>(MockBehavior.Strict);
        _mockGetFosterChild = new Mock<IGetFosterChildUseCase>(MockBehavior.Strict);
        _mockCreateFosterChild = new Mock<ICreateFosterChildUseCase>(MockBehavior.Strict);
        _mockUpdateFosterChild = new Mock<IUpdateFosterChildUseCase>(MockBehavior.Strict);
        _mockDeleteFosterChild = new Mock<IDeleteFosterChildUseCase>(MockBehavior.Strict);
        _mockAudit = new Mock<IAudit>(MockBehavior.Strict);

        var configData = new Dictionary<string, string?>
        {
            { "Jwt:Scopes:local_authority", "local_authority" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new FosterFamilyController(
            Mock.Of<ILogger<FosterFamilyController>>(),
            _configuration,
            _mockGetFosterFamily.Object,
            _mockCreateFosterFamily.Object,
            _mockUpdateFosterCarer.Object,
            _mockDeleteFosterCarer.Object,
            _mockDeleteFosterPartner.Object,
            _mockSearchFosterFamilies.Object,
            _mockGetFosterChild.Object,
            _mockCreateFosterChild.Object,
            _mockUpdateFosterChild.Object,
            _mockDeleteFosterChild.Object,
            _mockAudit.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockGetFosterFamily.VerifyAll();
        _mockCreateFosterFamily.VerifyAll();
        _mockUpdateFosterCarer.VerifyAll();
        _mockDeleteFosterCarer.VerifyAll();
        _mockDeleteFosterPartner.VerifyAll();
        _mockSearchFosterFamilies.VerifyAll();
        _mockGetFosterChild.VerifyAll();
        _mockCreateFosterChild.VerifyAll();
        _mockUpdateFosterChild.VerifyAll();
        _mockDeleteFosterChild.VerifyAll();
    }

    private void SetupControllerWithLocalAuthorityIds(List<int> ids)
    {
        var httpContext = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "unit-test-user")
        };

        if (ids.Any())
        {
            var scopeValue = ids.Contains(0)
                ? "local_authority"
                : string.Join(" ",
                    ids.Select(x => $"local_authority:{x}"));

            claims.Add(new Claim("scope", scopeValue));
        }

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims));

        _sut.ControllerContext =
            new ControllerContext
            {
                HttpContext = httpContext
            };
    }

    [Test]
    public async Task GetFosterFamily_Returns_Ok()
    {
        // Arrange
        var id = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var response = new FosterFamilyResponse
        {
            FosterCarerId = id
        };

        _mockGetFosterFamily
            .Setup(x => x.Execute(
                id,
                201,
                true))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.GetFosterFamily(
            id,
            true);  

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task GetFosterFamily_Returns_BadRequest_When_No_LA_Scope()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds([]);

        // Act
        var result = await _sut.GetFosterFamily(
            Guid.NewGuid(),
            false);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var error = (ErrorResponse)badRequest.Value!;

        error.Errors.First().Title
            .Should().Be("No local authority scope found");
    }

    [Test]
    public async Task GetFosterFamily_Returns_NotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetFosterFamily
            .Setup(x => x.Execute(
                id, 
                201,
                false))
            .ThrowsAsync(new NotFoundException());

        // Act
        var result = await _sut.GetFosterFamily(
            id,
            false);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task CreateFosterFamily_Returns_Created()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterFamilyRequest
        {
            FosterCarer = new FosterCarerRequest(),
            FosterChild = new FosterChildRequest()
        };

        var response = new FosterFamilyCreatedResponse
        {
            ChildName = "Tom Smith"
        };

        _mockCreateFosterFamily
            .Setup(x => x.Execute(
                request,
                201))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.CreateFosterFamily(
            request);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(201);
    }

    [Test]
    public async Task CreateFosterFamily_Returns_BadRequest_For_ValidationException()
    {
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterFamilyRequest();

        _mockCreateFosterFamily
            .Setup(x => x.Execute(
                request,
                201))
            .ThrowsAsync(
                new FluentValidation.ValidationException(
                    "Validation failed"));

        var result = await _sut.CreateFosterFamily(
            request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task DeleteFosterCarer_Returns_NoContent()
    {
        // Arrange
        var id = Guid.NewGuid();

        _mockDeleteFosterCarer
            .Setup(x => x.Execute(id))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteFosterCarer(id);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();

        ((StatusCodeResult)result)
            .StatusCode
            .Should()
            .Be(StatusCodes.Status204NoContent);
    }

    [Test]
    public async Task DeleteFosterCarer_Returns_NotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        _mockDeleteFosterCarer
            .Setup(x => x.Execute(id))
            .ThrowsAsync(
                new NotFoundException("not found"));

        // Act
        var result = await _sut.DeleteFosterCarer(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task GetFosterChild_Returns_Ok()
    {
        // Arrange
        var id = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var response = new FosterChildResponse
        {
            FosterChildId = id,
            ChildFullName = "Tom Smith"
        };

        _mockGetFosterChild
            .Setup(x => x.Execute(
                id,
                201,
                true))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.GetFosterChild(
            id,
            true);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task GetFosterChild_Returns_BadRequest_When_No_LocalAuthority_Scope()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds([]);

        // Act
        var result = await _sut.GetFosterChild(
            Guid.NewGuid());

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = badRequest.Value as ErrorResponse;

        errorResponse.Should().NotBeNull();

        errorResponse!.Errors.First().Title
            .Should().Be("No local authority scope found");
    }

    [Test]
    public async Task GetFosterChild_Returns_BadRequest_When_ValidationException_Thrown()
    {
        // Arrange
        var id = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockGetFosterChild
            .Setup(x => x.Execute(
                id,
                201,
                false))
            .ThrowsAsync(
                new ValidationException(
                    [new Error { Title = "Invalid foster child id" }],
                    "Validation failed"));

        // Act
        var result = await _sut.GetFosterChild(
            id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = badRequest.Value as ErrorResponse;

        errorResponse.Should().NotBeNull();

        errorResponse!.Errors.First().Title
            .Should().Be("Invalid foster child id");
    }

    [Test]
    public async Task CreateFosterChild_Returns_Created()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterChildRequest
        {
            ChildFirstName = "Tom",
            ChildLastName = "Smith",
            ChildDateOfBirth = new DateTime(2022, 1, 1),
            ChildPostCode = "AB1 2CD"
        };

        var response = new FosterChildCreatedResponse
        {
            ChildName = "Tom Smith",
            EligiblityCode = "ABC123",
            Status = "Active"
        };

        _mockCreateFosterChild
            .Setup(x => x.Execute(
                request,
                201,
                fosterCarerId,
                It.IsAny<DateTime>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.CreateFosterChild(
            fosterCarerId,
            request);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task CreateFosterChild_Returns_BadRequest_When_No_LocalAuthority_Scope()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds([]);

        var request = new FosterChildRequest();

        // Act
        var result = await _sut.CreateFosterChild(
            Guid.NewGuid(),
            request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = (ErrorResponse)badRequest.Value!;

        errorResponse.Errors.First().Title
            .Should().Be("No local authority scope found");
    }

    [Test]
    public async Task CreateFosterChild_Returns_NotFound_When_NotFoundException_Thrown()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterChildRequest();

        _mockCreateFosterChild
            .Setup(x => x.Execute(
                request,
                201,
                fosterCarerId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(
                new NotFoundException(
                    $"Foster carer {fosterCarerId} not found"));

        // Act
        var result = await _sut.CreateFosterChild(
            fosterCarerId,
            request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFound = (NotFoundObjectResult)result;

        var errorResponse = (ErrorResponse)notFound.Value!;

        errorResponse.Errors.First().Title
            .Should().Be($"Foster carer {fosterCarerId} not found");
    }

    [Test]
    public async Task CreateFosterChild_Returns_BadRequest_When_ArgumentNullException_Thrown()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        _mockCreateFosterChild
            .Setup(x => x.Execute(
                It.IsAny<FosterChildRequest>(),
                201,
                fosterCarerId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new ArgumentNullException("request"));

        // Act
        var result = await _sut.CreateFosterChild(
            fosterCarerId,
            null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task CreateFosterChild_Returns_BadRequest_When_FluentValidationException_Thrown()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterChildRequest();

        _mockCreateFosterChild
            .Setup(x => x.Execute(
                request,
                201,
                fosterCarerId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(
                new FluentValidation.ValidationException(
                    "Validation failed"));

        // Act
        var result = await _sut.CreateFosterChild(
            fosterCarerId,
            request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = (ErrorResponse)badRequest.Value!;

        errorResponse.Errors.First().Title
            .Should().Contain("Validation failed");
    }

    [Test]
    public async Task CreateFosterChild_Returns_BadRequest_When_Unexpected_Exception_Thrown()
    {
        // Arrange
        var fosterCarerId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new FosterChildRequest();

        _mockCreateFosterChild
            .Setup(x => x.Execute(
                request,
                201,
                fosterCarerId,
                It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Something went wrong"));

        // Act
        var result = await _sut.CreateFosterChild(
            fosterCarerId,
            request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = (ErrorResponse)badRequest.Value!;

        errorResponse.Errors.First().Title
            .Should().Be("Something went wrong");
    }

    [Test]
    public async Task UpdateFosterChild_Returns_Ok()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new UpdateFosterChildRequest
        {
            FosterChildRequest = new FosterChildRequest
            {
                ChildFirstName = "Tom",
                ChildLastName = "Smith",
                ChildDateOfBirth = new DateTime(2022, 1, 1),
                ChildPostCode = "AB1 2CD"
            }
        };

        var response = new FosterChildResponse
        {
            FosterChildId = fosterChildId,
            ChildFullName = "Tom Smith"
        };

        _mockUpdateFosterChild
            .Setup(x => x.Execute(
                fosterChildId,
                201,
                request))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.UpdateFosterChild(
            fosterChildId,
            request);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = (ObjectResult)result;

        objectResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Test]
    public async Task UpdateFosterChild_Returns_BadRequest_When_No_LocalAuthority_Scope()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds([]);

        var request = new UpdateFosterChildRequest();

        // Act
        var result = await _sut.UpdateFosterChild(
            Guid.NewGuid(),
            request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequest = (BadRequestObjectResult)result;

        var errorResponse = (ErrorResponse)badRequest.Value!;

        errorResponse.Errors.First().Title
            .Should().Be("No local authority scope found");
    }

    [Test]
    public async Task UpdateFosterChild_Returns_NotFound_When_NotFoundException_Thrown()
    {
        // Arrange
        var fosterChildId = Guid.NewGuid();

        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var request = new UpdateFosterChildRequest();

        _mockUpdateFosterChild
            .Setup(x => x.Execute(
                fosterChildId,
                201,
                request))
            .ThrowsAsync(
                new NotFoundException(
                    $"Foster child {fosterChildId} not found"));

        // Act
        var result = await _sut.UpdateFosterChild(
            fosterChildId,
            request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFound = (NotFoundObjectResult)result;

        var errorResponse = (ErrorResponse)notFound.Value!;

        errorResponse.Errors.First().Title
            .Should().Be($"Foster child {fosterChildId} not found");
    }

    [Test]
    public async Task SearchFosterFamilies_Returns_Multiple_Results()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var response = new FosterFamiliesSearchResponse
        {
            PageNumber = 1,
            PageSize = 10,
            TotalNumberOfRecords = 3,
            Data =
            [
                new FosterFamiliesSearchItemResponse
            {
                ChildName = "Tom Smith",
                CarerName = "John Smith",
                EligibilityCode = "ELIG001"
            },
            new FosterFamiliesSearchItemResponse
            {
                ChildName = "Jane Jones",
                CarerName = "Peter Jones",
                EligibilityCode = "ELIG002"
            },
            new FosterFamiliesSearchItemResponse
            {
                ChildName = "Sam Brown",
                CarerName = "Sarah Brown",
                EligibilityCode = "ELIG003"
            }
            ]
        };

        _mockSearchFosterFamilies
            .Setup(x => x.Execute(
                It.Is<FosterFamiliesSearchRequest>(r =>
                    r.PageNumber == 1 &&
                    r.PageSize == 10),
                201))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.SearchFosterFamilies(
            1,
            10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var okResult = (OkObjectResult)result;

        var returnedResponse =
            (FosterFamiliesSearchResponse)okResult.Value!;

        returnedResponse.TotalNumberOfRecords.Should().Be(3);

        returnedResponse.Data.Should().HaveCount(3);

        returnedResponse.Data.Select(x => x.ChildName)
            .Should()
            .ContainInOrder(
                "Tom Smith",
                "Jane Jones",
                "Sam Brown");
    }

    [Test]
    public async Task SearchFosterFamilies_Returns_Empty_Data()
    {
        // Arrange
        SetupControllerWithLocalAuthorityIds(new List<int> { 201 });

        var response = new FosterFamiliesSearchResponse
        {
            PageNumber = 1,
            PageSize = 10,
            TotalNumberOfRecords = 0,
            Data = []
        };

        _mockSearchFosterFamilies
            .Setup(x => x.Execute(
                It.Is<FosterFamiliesSearchRequest>(r =>
                    r.PageNumber == 1 &&
                    r.PageSize == 10),
                201))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.SearchFosterFamilies(
            1,
            10);

        // Assert
        var okResult = (OkObjectResult)result;

        var returned =
            (FosterFamiliesSearchResponse)okResult.Value!;

        returned.TotalNumberOfRecords.Should().Be(0);
        returned.Data.Should().BeEmpty();
    }

}