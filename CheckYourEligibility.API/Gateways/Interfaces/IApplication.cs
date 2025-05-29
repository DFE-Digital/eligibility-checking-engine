using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.Gateways.Interfaces;

public interface IApplication
{
    Task<ApplicationResponse> PostApplication(ApplicationRequestData data);
    Task<ApplicationResponse?> GetApplication(string guid);
    Task<ApplicationSearchResponse> GetApplications(ApplicationRequestSearch model);
    Task<ApplicationStatusUpdateResponse> UpdateApplicationStatus(string guid, ApplicationStatusData data);
    Task<int> GetLocalAuthorityIdForEstablishment(int establishmentId);
}