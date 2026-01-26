// Ignore Spelling: Fsm

using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IHash
{
    Task<EligibilityCheckHash?> Exists(CheckProcessData item);

    Task<string> Create(CheckProcessData item, CheckEligibilityStatus checkResult, ProcessEligibilityCheckSource source,
        AuditData auditDataTemplate, IEligibilityCheckContext? dbContextFactory = null);
}