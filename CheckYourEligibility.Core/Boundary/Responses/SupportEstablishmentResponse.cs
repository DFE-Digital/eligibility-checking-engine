using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Boundary.Responses
{
    public class SupportEstablishmentResponse
    {
        public Establishment Establishment { get; set; }

        public LocalAuthority? LocalAuthority { get; set; }

        public MultiAcademyTrust? MultiAcademyTrust { get; set; }
    }
}
