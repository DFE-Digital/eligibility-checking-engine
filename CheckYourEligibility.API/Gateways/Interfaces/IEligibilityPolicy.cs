using CheckYourEligibility.API.Domain;

namespace CheckYourEligibility.API.Gateways.Interfaces
{
    public interface IEligibilityPolicy
    {
        public Task<EligibilityPolicy?> GeEligibilityPolicyById(int EligibilityPolicyId);
    }
}
