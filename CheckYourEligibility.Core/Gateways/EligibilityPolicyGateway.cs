using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.Core.Gateways
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
