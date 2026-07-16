using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.Gateways;

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

    
    public async Task CreateOrUpdateUser(UserCreateRequest request)
    {
        var isApiUser = Enum.TryParse<UserType>(request.MetaData.Source, true, out var userType) && userType == UserType.API;

        User? existingUser;

        if (isApiUser)
        {
            // API user
            existingUser = _db.Users.FirstOrDefault(u =>
                u.UserName.ToLower() == request.MetaData.UserName.ToLower() &&
                u.OrganisationType.ToString() == request.MetaData.OrganisationType &&
                u.OrganisationId == request.MetaData.OrganisationID);
        }
        else
        {
            // Portal user
            existingUser = _db.Users.FirstOrDefault(u =>
                u.Email.ToLower() == request.Data.Email.ToLower() &&
                u.UserType == userType &&
                u.OrganisationType.ToString() == request.MetaData.OrganisationType &&
                u.OrganisationId == request.MetaData.OrganisationID);
        }

        if (existingUser != null)
        {
            existingUser.LastLogin = DateTime.UtcNow;

            _logger.LogInformation(
                "Updated last login for user {UserName}",
                SanitizeForLog(existingUser.UserName));
        }
        else
        {
            existingUser = new User
            {
                UserID = Guid.NewGuid().ToString(),
                Reference = string.Empty,
                UserName = request.MetaData.UserName,
                Email = request.Data.Email,
                UserType = isApiUser ? UserType.API : Enum.Parse<UserType>(request.MetaData.Source),
                OrganisationType = Enum.Parse<OrganisationType>(request.MetaData.OrganisationType),
                OrganisationId = request.MetaData.OrganisationID,
                LastLogin = DateTime.UtcNow
            };

            await _db.Users.AddAsync(existingUser);

            _logger.LogInformation(
                "Created user {UserName}",
                SanitizeForLog(existingUser.UserName));
        }

        await _db.SaveChangesAsync();
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
            x.Email.ToLower() == request.Data.Email.ToLower() && x.Reference.ToLower() == request.Data.Reference.ToLower());

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
        item.OrganisationType = Enum.Parse<OrganisationType>(request.MetaData.OrganisationType!);
        item.OrganisationId = request.MetaData.OrganisationID;

        item.UserID = Guid.NewGuid().ToString();

        await _db.Users.AddAsync(item);
        await _db.SaveChangesAsync();

        return item.UserID;
    }
}