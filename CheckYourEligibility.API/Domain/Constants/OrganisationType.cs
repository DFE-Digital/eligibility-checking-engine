namespace CheckYourEligibility.API.Domain.Constants
{
    /// <summary>
    /// What type of organisation is making the check
    /// It can be local-authority, establishment multi-academy-trust
    /// unspecified (if no organisation ID is passed in scope)
    /// ambiguous (if more than one  organisation ID is passed in scope)
    /// </summary>
    public static class OrganisationType
    {
        public const string local_authority = "local-authority";
        public const string multi_academy_trust = "multi-academy-trust";
        public const string establishment = "establishment";
        public const string unspecified = "unspecified";
        public const string ambiguous = "ambiguous";

    }
}
