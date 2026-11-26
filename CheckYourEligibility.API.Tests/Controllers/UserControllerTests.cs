using System.Security.Claims;
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
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Domain.Authorization;
using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.API.Tests.Controllers;

public class UserControllerTests : ControllerTestBase
{
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ICreateOrUpdateFSMParentUserUseCase> _mockCreateOrUpdateUserUseCase;
    private Mock<IGetUserRolesUseCase> _mockGetUserRolesUseCase;
    private Mock<IAddUserRoleUseCase> _mockAddUserRoleUseCase;
    private Mock<IRemoveUserRoleUseCase> _mockRemoveUserRoleUseCase;
    private Mock<ISearchUsersUseCase> _mockSearchUsersUseCase;
    private Mock<IGetUserUseCase> _mockGetUserUseCase;
    private Mock<IUpdateUserEnabledUseCase> _mockUpdateUserEnabledUseCase;
    private ILogger<UserController> _mockLogger;
    private UserController _sut;

    [SetUp]
    public void Setup()
    {
        _mockCreateOrUpdateUserUseCase = new Mock<ICreateOrUpdateFSMParentUserUseCase>(MockBehavior.Strict);
        _mockGetUserRolesUseCase = new Mock<IGetUserRolesUseCase>(MockBehavior.Strict);
        _mockAddUserRoleUseCase = new Mock<IAddUserRoleUseCase>(MockBehavior.Strict);
        _mockRemoveUserRoleUseCase = new Mock<IRemoveUserRoleUseCase>(MockBehavior.Strict);
        _mockSearchUsersUseCase = new Mock<ISearchUsersUseCase>(MockBehavior.Strict);
        _mockGetUserUseCase = new Mock<IGetUserUseCase>(MockBehavior.Strict);
        _mockUpdateUserEnabledUseCase = new Mock<IUpdateUserEnabledUseCase>(MockBehavior.Strict);
        _mockLogger = Mock.Of<ILogger<UserController>>();
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new UserController(
            _mockLogger,
            _mockCreateOrUpdateUserUseCase.Object,
            _mockGetUserRolesUseCase.Object,
            _mockAddUserRoleUseCase.Object,
            _mockRemoveUserRoleUseCase.Object,
            _mockSearchUsersUseCase.Object,
            _mockGetUserUseCase.Object,
            _mockUpdateUserEnabledUseCase.Object,
            _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockCreateOrUpdateUserUseCase.VerifyAll();
        _mockGetUserRolesUseCase.VerifyAll();
        _mockAddUserRoleUseCase.VerifyAll();
        _mockRemoveUserRoleUseCase.VerifyAll();
    }

    [Test]
    public async Task Given_valid_Request_Post_Should_Return_Status201Created()
    {
        // Arrange
        var request = _fixture.Create<UserCreateRequest>();

        var response = _fixture.Create<UserSaveItemResponse>();

        _mockCreateOrUpdateUserUseCase
            .Setup(x => x.Execute(It.IsAny<UserCreateRequest>()))
            .ReturnsAsync(response);

        var httpContext = new DefaultHttpContext();

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "FreeSchoolMealsAdmin:test@test.com")
                },
                "Test"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _sut.FsmParentUserPost(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();

        var objectResult = result as ObjectResult;

        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().BeEquivalentTo(response);

        _mockCreateOrUpdateUserUseCase.Verify(
            x => x.Execute(It.IsAny<UserCreateRequest>()),
            Times.Once);
    }

    [Test]
    public async Task Given_Null_Request_Should_Return_Status400BadRequest()
    {
        // Act
        var result = await _sut.FsmParentUserPost(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task SearchUsers_Should_Return_Ok()
    {
        var response = new UsersSearchResponse { PageNumber = 1, PageSize = 10 };
        _mockSearchUsersUseCase.Setup(x => x.Execute("a", UserType.API, 1, 10)).ReturnsAsync(response);

        var result = await _sut.SearchUsers("a", UserType.API, 1, 10);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Test]
    public async Task GetUser_Should_Return_Ok()
    {
        var response = new UserDetailsResponse();
        _mockGetUserUseCase.Setup(x => x.Execute("1")).ReturnsAsync(response);

        var result = await _sut.GetUser("1");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Test]
    public async Task GetUser_Unknown_Should_Return_NotFound()
    {
        _mockGetUserUseCase.Setup(x => x.Execute("1")).ThrowsAsync(new NotFoundException("missing"));

        var result = await _sut.GetUser("1");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public async Task PatchUser_Should_Return_NoContent()
    {
        _mockUpdateUserEnabledUseCase.Setup(x => x.Execute("1", false)).Returns(Task.CompletedTask);

        var result = await _sut.PatchUser("1", new UserPatchRequest { Data = new UserPatchData { IsEnabled = false } });

        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task PatchUser_Missing_IsEnabled_Should_Return_BadRequest()
    {
        var result = await _sut.PatchUser("1", new UserPatchRequest { Data = new UserPatchData() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task PatchUser_Unknown_Should_Return_NotFound()
    {
        _mockUpdateUserEnabledUseCase.Setup(x => x.Execute("1", true)).ThrowsAsync(new NotFoundException("missing"));

        var result = await _sut.PatchUser("1", new UserPatchRequest { Data = new UserPatchData { IsEnabled = true } });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Test]
    public void UserEnums_Should_Serialise_As_Strings_And_Accept_Strings_Or_Numbers()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(
            new UserRoleItemResponse { RoleName = UserRoleName.Support_UserManagement });

        json.Should().Contain("\"Support_UserManagement\"");
        Newtonsoft.Json.JsonConvert.DeserializeObject<UserType>("\"API\"").Should().Be(UserType.API);
        Newtonsoft.Json.JsonConvert.DeserializeObject<UserType>("5").Should().Be(UserType.API);
    }

    [TestCase(nameof(UserController.SearchUsers))]
    [TestCase(nameof(UserController.GetUser))]
    [TestCase(nameof(UserController.PatchUser))]
    public void New_Endpoints_Should_Require_UserManagement_Policy(string method)
    {
        var attr = typeof(UserController).GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be(PolicyNames.RequireUserManagementRole);
    }
}