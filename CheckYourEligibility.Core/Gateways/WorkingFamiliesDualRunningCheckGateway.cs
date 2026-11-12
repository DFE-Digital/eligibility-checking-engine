using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Gateways
{
    public interface IWorkingFamiliesDualRunningCheck {
        Task Create(WorkingFamiliesDualRunningCheck dualRunningCheck, EligibilityCheckContext dbContextFactory = null);
    }
    public class WorkingFamiliesDualRunningCheckGateway : IWorkingFamiliesDualRunningCheck
    {
        private readonly IEligibilityCheckContext _db;
        public WorkingFamiliesDualRunningCheckGateway(IEligibilityCheckContext dbContext)
        {
            _db = dbContext;
        }
        public async Task Create(WorkingFamiliesDualRunningCheck dualRunningCheck, EligibilityCheckContext dbContextFactory = null)
        {
            var context = dbContextFactory ?? _db;
            await context.WorkingFamiliesDualRunningChecks.AddAsync(dualRunningCheck);
        
        }
    }
}
