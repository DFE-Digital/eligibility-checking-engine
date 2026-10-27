using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class SupportEstablishmentResponse
    {
        public Establishment Establishment { get; set; }

        public LocalAuthority? LocalAuthority { get; set; }

        public MultiAcademyTrust? MultiAcademyTrust { get; set; }
    }
}
