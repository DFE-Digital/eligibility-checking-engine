using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
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
        public async Task<EligibilityPolicy?> GeEligibilityPolicyByIdAsync(int? EligibilityPolicyId, EligibilityCheckContext dbContextFactory = null)
        {
            var context = dbContextFactory ?? _db;
            return await context.EligibilityPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == EligibilityPolicyId && x.IsDeleted == false);
            
        }
    

    }
}
