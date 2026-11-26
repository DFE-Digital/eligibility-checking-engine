using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Database;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IAudit
{
    Task<string> AuditAdd(AuditData auditData, EligibilityCheckContext dbContextFactory = null);
    AuditData? AuditDataGet(AuditType type, string id);
    Task<string> CreateAuditEntry(AuditType type, string id, EligibilityCheckContext dbContextFactory = null);
}