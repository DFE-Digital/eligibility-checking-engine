namespace CheckYourEligibility.API.Domain.Constants;

/// <summary>
///     Authorization policy name constants
/// </summary>
public static class PolicyNames
{
    public const string RequireLocalAuthorityScope = "RequireLocalAuthorityScope";
    public const string RequireCheckScope = "RequireCheckScope";
    public const string RequireApplicationScope = "RequireApplicationScope";
    public const string RequireAdminScope = "RequireAdminScope";
    public const string RequireBulkCheckScope = "RequireBulkCheckScope";
    public const string RequireEstablishmentScope = "RequireEstablishmentScope";
    public const string RequireUserScope = "RequireUserScope";
    public const string RequireEngineScope = "RequireEngineScope";
    public const string RequireNotificationScope = "RequireNotificationScope";
    public const string RequireMultiAcademyTrustScope = "RequireMultiAcademyTrustScope";
    public const string RequireLaOrMatScope = "RequireLaOrMatScope";
    public const string RequireLaOrMatOrSchoolScope = "RequireLaOrMatOrSchoolScope";
    public const string RequireLaOrAdminScope = "RequireLaOrAdminScope";
}