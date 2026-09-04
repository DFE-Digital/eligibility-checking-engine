using System.Net;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Authorization;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Extensions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
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
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger, ICreateOrUpdateFSMParentUserUseCase createOrUpdateUserUseCase,
        IGetUserRolesUseCase getUserRolesUseCase, IAddUserRoleUseCase addUserRoleUseCase,
        IRemoveUserRoleUseCase removeUserRoleUseCase, IAudit audit)
        : base(audit)
    {
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

        model.MetaData = HttpContext.User.CalculateMetaData();

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

        if(!Guid.TryParse(userId, out _))
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
}