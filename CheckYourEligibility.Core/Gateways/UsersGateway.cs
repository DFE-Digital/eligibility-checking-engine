using AutoMapper;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.Core.Gateways;

public class UsersGateway : IUsers
{
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;

    public UsersGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper)
    {
        _logger = logger.CreateLogger("UsersService");
        _db = dbContext;
        _mapper = mapper;
    }

    private static string SanitizeForLog(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

    /// <summary>
    ///  Creates a new user if one does not already exist, otherwise updates the user's last login timestamp
    ///  API users are matched using UserName, OrganisationType and OrganisationId
    ///  Portal users are matched using Email, UserType, OrganisationType and  OrganisationId
    ///  </summary> <param name="request">
    ///  Details of the user to create or update </param> 
    ///  <returns> The GUID of the created user </returns>
    public async Task<string> CreateOrUpdateUser(UserCreateRequest request)
    {
        var isApiUser =
            Enum.TryParse<UserType>(request.MetaData.Source, true, out var userType)
            && userType == UserType.API;

        var organisationType =
            Enum.Parse<OrganisationType>(request.MetaData.OrganisationType);

        User? user;

        if (isApiUser)
        {
            user = await _db.Users.FirstOrDefaultAsync(u =>
                u.UserName.ToLower() == request.MetaData.UserName.ToLower() &&
                u.UserType == UserType.API &&
                u.OrganisationType == organisationType &&
                u.OrganisationId == request.MetaData.OrganisationID);
        }
        else
        {
            user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == request.Data.Email.ToLower() &&
                u.UserType == userType &&
                u.OrganisationType == organisationType &&
                u.OrganisationId == request.MetaData.OrganisationID);
        }

        if (user != null)
        {
            user.LastLogin = DateTime.UtcNow;

            _logger.LogInformation(
                "Updated last login for user {UserName}",
                SanitizeForLog(user.UserName));
        }
        else
        {
            user = new User
            {
                UserID = Guid.NewGuid().ToString(),
                Reference = request.Data.Reference,
                UserName = request.MetaData.UserName,
                Email = request.Data.Email,
                UserType = isApiUser ? UserType.API : userType,
                OrganisationType = organisationType,
                OrganisationId = request.MetaData.OrganisationID,
                LastLogin = DateTime.UtcNow
            };

            await _db.Users.AddAsync(user);

            _logger.LogInformation(
                "Created user {UserName}",
                SanitizeForLog(user.UserName));
        }

        await _db.SaveChangesAsync();

        return user.UserID;
    }



    /// <summary>
    ///     Creates a user, note this can not be tested on existing users exception
    ///     due to limitations on in memory db.
    ///     Note: this is for FSM Parent only.
    /// </summary>
    /// <param name="request">The user create request.</param>
    /// <returns>The user ID.</returns>
    public async Task<string> CreateOrUpdateFSMParentUser(UserCreateRequest request)
    {
        var existingUser = _db.Users.FirstOrDefault(x =>
            x.Email.ToLower() == request.Data.Email.ToLower()
            && x.Reference.ToLower() == request.Data.Reference.ToLower()
            && x.UserType == UserType.FreeSchoolMealsParent);

        if (existingUser != null)
        {

            existingUser.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return existingUser.UserID;
        }

        var item = _mapper.Map<User>(request.Data);

        // map metadata where exists
        item.LastLogin = DateTime.UtcNow;
        item.UserType = UserType.FreeSchoolMealsParent; // will always be fsm parent
        item.UserName = request.MetaData.UserName;
        item.OrganisationType = Enum.Parse<OrganisationType>(
            request.MetaData.OrganisationType ?? OrganisationType.none.ToString());
        item.OrganisationId = request.MetaData.OrganisationID;

        item.UserID = Guid.NewGuid().ToString();

        await _db.Users.AddAsync(item);
        await _db.SaveChangesAsync();

        return item.UserID;
    }

    public async Task<IList<UserRole>> GetUserRoles(string userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserID == userId);
        if (user is null)
        {
            throw new NotFoundException($"User {userId} not found");
        }

        return await _db.UserRoles
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RoleName)
            .ToListAsync();
    }

    public async Task<(IList<User> Users, int TotalRecords)> SearchUsers(string? search, UserType? userType, int pageNumber, int pageSize)
    {
        IQueryable<User> query = _db.Users;

        if (userType.HasValue)
        {
            query = query.Where(u => u.UserType == userType.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.UserName).ThenBy(u => u.UserID)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, total);
    }

    public async Task<(User User, IList<UserRole> Roles)> GetUser(string userId)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserID == userId);
        if (user is null)
        {
            throw new NotFoundException($"User {userId} not found");
        }

        var roles = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RoleName)
            .ToListAsync();

        return (user, roles);
    }

    public async Task UpdateUserEnabled(string userId, bool isEnabled)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserID == userId);
        if (user is null)
        {
            throw new NotFoundException($"User {userId} not found");
        }

        user.IsEnabled = isEnabled;
        await _db.SaveChangesAsync();
    }

    public async Task<UserRole> AddUserRole(string userId, UserRoleName roleName)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserID == userId);
        if (user is null)
        {
            throw new NotFoundException($"User {userId} not found");
        }

        var existingRole = await _db.UserRoles
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleName == roleName);

        if (existingRole != null) { return existingRole; }

        var role = new UserRole
        {
            UserId = userId,
            RoleName = roleName
        };

        await _db.UserRoles.AddAsync(role);
        await _db.SaveChangesAsync();

        return role;
    }

    public async Task<bool> RemoveUserRole(string userId, UserRoleName roleName)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.UserID == userId);
        if (user is null)
        {
            throw new NotFoundException($"User {userId} not found");
        }

        var role = await _db.UserRoles.FirstOrDefaultAsync(x => x.UserId == userId && x.RoleName == roleName);

        if (role == null) { return false; }

        _db.UserRoles.Remove(role);
        await _db.SaveChangesAsync();

        return true;
    }
}