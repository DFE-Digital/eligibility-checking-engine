using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests.Controllers
{
    [TestFixture]
    public class GetAllBulkChecksControllerTests
    {
        private Mock<IGetAllBulkChecksUseCase> _mockUseCase = null!;
        private BulkCheckController _controller = null!;
        private Mock<ILogger<BulkCheckController>> _mockLogger = null!;
        private readonly Mock<IGetBulkCheckSummaryUseCase> _mockBulkCheckSummaryUseCase = new();

        [SetUp]
        public void Setup()
        {
            _mockUseCase = new Mock<IGetAllBulkChecksUseCase>();
            _mockLogger = new Mock<ILogger<BulkCheckController>>();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Scopes:local_authority", "local_authority" }
                })
                .Build();

            // Create minimal mocks - only create the ones that are actually needed
            var mockAudit = new Mock<IAudit>();

            _controller = new BulkCheckController(
                _mockLogger.Object,
                mockAudit.Object,
                configuration,
                null!, null!, null!, null!, null!,
                _mockUseCase.Object,
                _mockBulkCheckSummaryUseCase.Object
            );
        }

        [Test]
        public async Task GetAllBulkChecks_WithValidAdminUser_ReturnsOkResult()
        {
            // Arrange
            var expectedResponse = new CheckEligibilityBulkStatusesResponse
            {
                Checks = new List<BulkCheck>
                {
                    new BulkCheck
                    {
                        Id = "test-guid-1",
                        SubmittedDate = DateTime.UtcNow,
                        EligibilityType = "FreeSchoolMeals",
                        Status = "Complete",
                        Filename = "test-file-1.csv",
                        SubmittedBy = "admin@test.com",
                        Get_BulkCheck_Results = "/bulk-check/test-guid-1/results"
                    }
                }
            };

            _mockUseCase.Setup(x => x.Execute(
                It.Is<IList<int>>(ids => ids.Contains(0)),
                It.IsAny<CheckMetaData>()))
                .ReturnsAsync(expectedResponse);

            // Set up admin user context
            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority"),
                new Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    "test-admin")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetAllBulkChecks();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = (ObjectResult)result;
            Assert.That(objectResult.StatusCode, Is.EqualTo(200));
            Assert.That(objectResult.Value, Is.InstanceOf<CheckEligibilityBulkStatusesResponse>());

            var response = (CheckEligibilityBulkStatusesResponse)objectResult.Value!;
            Assert.That(response.Checks.Count(), Is.EqualTo(1));
            Assert.That(response.Checks.First().Id, Is.EqualTo("test-guid-1"));
        }

        [Test]
        public async Task GetAllBulkChecks_WithNoScopes_ReturnsUnauth()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id")
            };

            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetAllBulkChecks();

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
            var badRequestResult = (UnauthorizedObjectResult)result;
            Assert.That(badRequestResult.Value, Is.InstanceOf<ErrorResponse>());

            var errorResponse = (ErrorResponse)badRequestResult.Value!;
            Assert.That(errorResponse.Errors.First().Title, Is.EqualTo("Not authorised for local authority in scope"));
        }

        [Test]
        public async Task GetAllBulkChecks_WithSpecificLocalAuthorityScope_CallsUseCaseWithCorrectIds()
        {
            // Arrange
            var expectedResponse = new CheckEligibilityBulkStatusesResponse
            {
                Checks = new List<BulkCheck>()
            };

            _mockUseCase.Setup(x => x.Execute(
                It.Is<IList<int>>(ids => ids.Contains(123) && !ids.Contains(0)),
                It.IsAny<CheckMetaData>()))
                .ReturnsAsync(expectedResponse);

            // Set up user context with specific local authority
            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority:123"),
                new Claim("client_id", "test-client"),
                new Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    "test-user")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetAllBulkChecks();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            _mockUseCase.Verify(x => x.Execute(
                It.Is<IList<int>>(ids => ids.Contains(123) && !ids.Contains(0)),
                It.IsAny<CheckMetaData>()), Times.Once);
        }

        [Test]
        public async Task GetBulkCheckSummary_WhenGuidDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var bulkCheckId = Guid.NewGuid();

            _mockBulkCheckSummaryUseCase
                .Setup(x => x.Execute(
                    bulkCheckId,
                    It.IsAny<IList<int>>(),
                    It.IsAny<CheckMetaData>()))
                .ThrowsAsync(new NotFoundException());

            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority:123"),
                new Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    "free-school-meals-admin:test-user@test.com")
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            // Act
            var result = await _controller.GetBulkCheckSummary(bulkCheckId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetBulkCheckSummary_WhenBulkCheckExists_ReturnsOkResult()
        {
            // Arrange
            var bulkCheckId = Guid.NewGuid();

            var expectedResponse = new BulkCheckSummaryResponse
            {
                Filename = "test.csv",
                Status = "Complete",
                SubmittedDate = DateTime.UtcNow,
                SubmittedBy = "test-user@test.com",
                Outcomes = new Dictionary<string, int>
                {
                    { "eligible", 5 },
                    { "eligible-targeted", 10 },
                    { "notEligible", 2 }
                }
            };

            _mockBulkCheckSummaryUseCase
                .Setup(x => x.Execute(
                    bulkCheckId,
                    It.IsAny<IList<int>>(),
                    It.IsAny<CheckMetaData>()))
                .ReturnsAsync(expectedResponse);

            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority:123"),
                new Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    "free-school-meals-admin:test-user@test.com")
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            // Act
            var result = await _controller.GetBulkCheckSummary(bulkCheckId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());

            var objectResult = (ObjectResult)result;

            Assert.That(objectResult.StatusCode, Is.EqualTo(200));
            Assert.That(objectResult.Value, Is.InstanceOf<BulkCheckSummaryResponse>());

            var response = (BulkCheckSummaryResponse)objectResult.Value!;

            Assert.That(response.Filename, Is.EqualTo("test.csv"));
            Assert.That(response.Outcomes["eligible"], Is.EqualTo(5));
            Assert.That(response.Outcomes["eligible-targeted"], Is.EqualTo(10));
            Assert.That(response.Outcomes["notEligible"], Is.EqualTo(2));
        }
    }
}