using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways
{
    public class EligibilityPolicyGateway:IEligibilityPolicy
    {
       
       private readonly IEligibilityCheckContext _db;

        public EligibilityPolicyGateway(IEligibilityCheckContext db)
        { 
            _db = db;
        }
        public Task<EligibilityPolicy?> GeEligibilityPolicyById(int EligibilityPolicyId)
        {
            return _db.EligibilityPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == EligibilityPolicyId);
        }


    }
}
