using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Controllers;
using CheckYourEligibility.API.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests.Controllers
{
    [TestFixture]
    public class GetAllBulkChecksControllerTests
    {
        private Mock<IGetAllBulkChecksUseCase> _mockUseCase = null!;
        private EligibilityCheckController _controller = null!;
        private Mock<ILogger<EligibilityCheckController>> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockUseCase = new Mock<IGetAllBulkChecksUseCase>();
            _mockLogger = new Mock<ILogger<EligibilityCheckController>>();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Scopes:local_authority", "local_authority" }
                })
                .Build();

            // Create minimal mocks - only create the ones that are actually needed
            var mockAudit = new Mock<CheckYourEligibility.API.Gateways.Interfaces.IAudit>();

            _controller = new EligibilityCheckController(
                _mockLogger.Object,
                mockAudit.Object,
                configuration,
                // Pass nulls for dependencies we're not testing
                null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
                _mockUseCase.Object
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
                        Guid = "test-guid-1",
                        SubmittedDate = DateTime.UtcNow,
                        EligibilityType = "FreeSchoolMeals",
                        Status = "Complete",
                        Filename = "test-file-1.csv",
                        SubmittedBy = "admin@test.com",
                        Get_BulkCheck_Results = "/bulk-check/test-guid-1/results"
                    }
                }
            };

            _mockUseCase.Setup(x => x.Execute(It.Is<IList<int>>(ids => ids.Contains(0))))
                .ReturnsAsync(expectedResponse);

            // Set up admin user context
            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority")
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
            Assert.That(response.Checks.First().Guid, Is.EqualTo("test-guid-1"));
        }

        [Test]
        public async Task GetAllBulkChecks_WithNoScopes_ReturnsBadRequest()
        {
            // Arrange - user with no local authority scopes
            var claims = new List<Claim>();
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
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.That(badRequestResult.Value, Is.InstanceOf<ErrorResponse>());

            var errorResponse = (ErrorResponse)badRequestResult.Value!;
            Assert.That(errorResponse.Errors.First().Title, Is.EqualTo("No local authority scope found"));
        }

        [Test]
        public async Task GetAllBulkChecks_WithSpecificLocalAuthorityScope_CallsUseCaseWithCorrectIds()
        {
            // Arrange
            var expectedResponse = new CheckEligibilityBulkStatusesResponse
            {
                Checks = new List<BulkCheck>()
            };

            _mockUseCase.Setup(x => x.Execute(It.IsAny<IList<int>>()))
                .ReturnsAsync(expectedResponse);

            // Set up user context with specific local authority
            var claims = new List<Claim>
            {
                new Claim("scope", "local_authority:123")
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
            _mockUseCase.Verify(x => x.Execute(It.Is<IList<int>>(ids => ids.Contains(123) && !ids.Contains(0))), Times.Once);
        }
    }
}
