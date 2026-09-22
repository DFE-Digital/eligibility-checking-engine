using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class JwtAuthResponse
{
    public string access_token { get; set; }

    public int expires_in { get; set; }

    public string token_type { get; set; }

    public UserType? UserType { get; set; }

    public string? UserId { get; set; }

    public int? OrganisationId { get; set; }

    public OrganisationType? OrganisationType { get; set; }

    public List<UserRoleName> Roles { get; set; }
}