// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IAudit
{
    Task<string> AuditAdd(AuditData auditData, IEligibilityCheckContext? dbContextFactory = null);
    AuditData? AuditDataGet(AuditType type, string id);
    Task<string> CreateAuditEntry(AuditType type, string id, IEligibilityCheckContext? dbContextFactory = null);
}