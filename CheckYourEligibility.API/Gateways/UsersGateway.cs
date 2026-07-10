using System.Text.RegularExpressions;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class UsersGateway : IUsers
{
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public UsersGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext)
    {
        _logger = logger.CreateLogger("UsersService");
        _db = dbContext;
    }

    /// <summary>
    /// Creates a new user if one does not already exist for the supplied
    /// email and reference. If the user already exists, their last login
    /// timestamp is updated and the existing user ID is returned.
    /// </summary>
    /// <param name="userCreateRequest">
    /// The user details and metadata used to create or update the user.
    /// </param>
    /// <returns>
    /// The unique identifier of the created or existing user.
    /// </returns>
    public async Task<string> Create(UserCreateRequest userCreateRequest)
    {
        var existingUser = await _db.Users
        .FirstOrDefaultAsync(x =>
            x.Email.ToLower() == userCreateRequest.Data.Email.ToLower() &&
            x.Reference.ToLower() == userCreateRequest.Data.Reference.ToLower());

        if (existingUser != null)
        {
            // update last login to now
            existingUser.LastLogin = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // return id
            return existingUser.UserID;
        }

        // new user
        var newUser = new User()
        {
            UserID = Guid.NewGuid().ToString(),

            UserType = userCreateRequest.metaData.Source switch
            {
                var source when Regex.IsMatch(source, "prod", RegexOptions.IgnoreCase) => UserType.API, // if api user, source contains prod
                "childcare-admin" => UserType.ChildcareAdmin,
                "free-school-meals-admin" => UserType.FreeSchoolMealsAdmin,
                "free-school-meals-frontend" => UserType.FreeSchoolMealsParent,
                 _ => throw new UserSaveException($"Unknown source '{userCreateRequest.metaData.Source}'")
            },

            LastLogin = DateTime.UtcNow,
            Email = userCreateRequest.Data.Email,
            Reference = userCreateRequest.metaData.Source.Contains("prod") ? "" : userCreateRequest.Data.Reference, // if api user, no reference 
            UserName = userCreateRequest.Data.Email,

            OrganisationType = userCreateRequest.metaData.OrganisationType switch
            {
                "local-authority" => OrganisationType.local_authority,
                "multi-academy-trust" => OrganisationType.multi_academy_trust,
                "establishment" => OrganisationType.establishment,
                _ => throw new UserSaveException($"Unknown organisation type '{userCreateRequest.metaData.OrganisationType}'")
            },

            OrganisationId = userCreateRequest.metaData.OrganisationID,
        };

        try
        {
            await _db.Users.AddAsync(newUser);
            await _db.SaveChangesAsync();

        }
        catch (DbUpdateException ex)
        {
            var sanitizedEmail = (newUser.Email ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            _logger.LogError(ex, "Failed to create user {Email}", sanitizedEmail);

            throw new UserSaveException(
                $"Failed to create or update user '{newUser.Email}'",
                ex);
        }

        return newUser.UserID;
    }
}