using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Database;

namespace CheckYourEligibility.Core.Gateways.Interfaces
{
    public interface IEligibilityPolicy
    {
        public Task<EligibilityPolicy> GeEligibilityPolicyByIdAsync(int? EligibilityPolicyId, EligibilityCheckContext dbContextFactory = null);
    }
}
