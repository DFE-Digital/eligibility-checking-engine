using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using BulkCheck = CheckYourEligibility.API.Domain.BulkCheck;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface ICheckingEngine
{
    Task<CheckEligibilityStatus?> ProcessCheck(string guid, AuditData? auditItem);
}