namespace CheckYourEligibility.API.Domain.Constants
{
    /// <summary>
    /// What type of organisation is making the check
    /// It can be local-authority, establishment multi-academy-trust
    public static class OrganisationType
    {
        public const string local_authority = "local-authority";
        public const string multi_academy_trust = "multi-academy-trust";
        public const string establishment = "establishment";

    }
}
