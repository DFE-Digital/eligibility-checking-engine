using System.Net;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Authorization;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Extensions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserController : BaseController
{
    private readonly ICreateOrUpdateFSMParentUserUseCase _createOrUpdateUserUseCase;
    private readonly IGetUserRolesUseCase _getUserRolesUseCase;
    private readonly IAddUserRoleUseCase _addUserRoleUseCase;
    private readonly IRemoveUserRoleUseCase _removeUserRoleUseCase;
    private readonly ISearchUsersUseCase _searchUsersUseCase;
    private readonly IGetUserUseCase _getUserUseCase;
    private readonly IUpdateUserEnabledUseCase _updateUserEnabledUseCase;
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger, ICreateOrUpdateFSMParentUserUseCase createOrUpdateUserUseCase,
        IGetUserRolesUseCase getUserRolesUseCase, IAddUserRoleUseCase addUserRoleUseCase,
        IRemoveUserRoleUseCase removeUserRoleUseCase, ISearchUsersUseCase searchUsersUseCase,
        IGetUserUseCase getUserUseCase, IUpdateUserEnabledUseCase updateUserEnabledUseCase, IAudit audit)
        : base(audit)
    {
        _searchUsersUseCase = searchUsersUseCase;
        _getUserUseCase = getUserUseCase;
        _updateUserEnabledUseCase = updateUserEnabledUseCase;
        _logger = logger;
        _createOrUpdateUserUseCase = createOrUpdateUserUseCase;
        _getUserRolesUseCase = getUserRolesUseCase;
        _addUserRoleUseCase = addUserRoleUseCase;
        _removeUserRoleUseCase = removeUserRoleUseCase;
    }

    /// <summary>
    ///     creates or returns existing user Id for fsm parent portal
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(UserSaveItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [Consumes("application/json", "application/vnd.api+json;version=1.0")]
    [HttpPost("/user")]
    [Authorize(Policy = PolicyNames.RequireUserScope)]
    public async Task<ActionResult> FsmParentUserPost([FromBody] UserCreateRequest model)
    {
        if (model == null || model.Data == null)
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "Invalid request, data is required." }]
            });
        }

        // Calculate best user metadata from all available sources
        var contextMetadata = HttpContext.User.CalculateMetaData();
        if (model.MetaData == null)
        {
            model.MetaData = contextMetadata;
        }
        else
        {
            if (string.IsNullOrEmpty(model.MetaData.UserName)) { model.MetaData.UserName = contextMetadata.UserName; }
            if (string.IsNullOrEmpty(model.MetaData.Source)) { model.MetaData.Source = contextMetadata.Source; }
            if (string.IsNullOrEmpty(model.MetaData.OrganisationType)) { model.MetaData.OrganisationType = contextMetadata.OrganisationType; }
        }

        var response = await _createOrUpdateUserUseCase.Execute(model);

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    /// <summary>
    ///     Gets roles for a specific user id.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(UserRolesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpGet("/user/{userId}/roles")]
    [Authorize(Policy = PolicyNames.RequireUserScope)]
    public async Task<ActionResult> GetUserRoles(string userId)
    {
        try
        {
            var response = await _getUserRolesUseCase.Execute(userId);

            if (response == null || response.Data == null || !response.Data.Any())
            {
                return Ok(new UserRolesResponse { Data = [] });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user roles for user {UserId}", userId);
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Adds a role assignment for a user.
    /// </summary>
    [ProducesResponseType(typeof(UserRoleItemResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [HttpPost("/user/{userId}/roles")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> AddUserRole(string userId, [FromBody] UserRoleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "Role name is required." }]
            });
        }

        if (!Enum.TryParse<UserRoleName>(request.RoleName, true, out var roleName))
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = $"Role name '{request.RoleName}' is not valid." }]
            });
        }

        if (!Guid.TryParse(userId, out _))
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = $"User ID '{userId}' is not a valid GUID." }]
            });
        }

        var response = await _addUserRoleUseCase.Execute(userId, roleName);

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    /// <summary>
    ///     Removes a role assignment for a user.
    /// </summary>
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [HttpDelete("/user/{userId}/roles/{roleName}")]
    [Authorize(Policy = PolicyNames.RequireAdminScope)]
    public async Task<ActionResult> RemoveUserRole(string userId, string roleName)
    {
        if (!Enum.TryParse<UserRoleName>(roleName, true, out var parsedRoleName))
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = $"Role name '{roleName}' is not valid." }]
            });
        }

        var removed = await _removeUserRoleUseCase.Execute(userId, parsedRoleName);

        if (!removed)
        {
            return NotFound(new ErrorResponse
            {
                Errors = [new Error { Title = "Role assignment not found." }]
            });
        }

        return NoContent();
    }

    /// <summary>
    ///     Paginated search of users by user name or email.
    /// </summary>
    [ProducesResponseType(typeof(UsersSearchResponse), (int)HttpStatusCode.OK)]
    [HttpGet("/users/search")]
    [Authorize(Policy = PolicyNames.RequireUserManagementRole)]
    public async Task<ActionResult> SearchUsers([FromQuery] string? search, [FromQuery] UserType? userType,
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        return Ok(await _searchUsersUseCase.Execute(search, userType, pageNumber, pageSize));
    }

    /// <summary>
    ///     Gets a user and their roles.
    /// </summary>
    [ProducesResponseType(typeof(UserDetailsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpGet("/users/{userId}")]
    [Authorize(Policy = PolicyNames.RequireUserManagementRole)]
    public async Task<ActionResult> GetUser(string userId)
    {
        try
        {
            return Ok(await _getUserUseCase.Execute(userId));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }

    /// <summary>
    ///     Updates the IsEnabled flag of a user.
    /// </summary>
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [HttpPatch("/users/{userId}")]
    [Authorize(Policy = PolicyNames.RequireUserManagementRole)]
    public async Task<ActionResult> PatchUser(string userId, [FromBody] UserPatchRequest request)
    {
        if (request?.Data?.IsEnabled == null)
        {
            return BadRequest(new ErrorResponse
            {
                Errors = [new Error { Title = "IsEnabled is required." }]
            });
        }

        try
        {
            await _updateUserEnabledUseCase.Execute(userId, request.Data.IsEnabled.Value);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponse { Errors = [new Error { Title = ex.Message }] });
        }
    }
}