using CheckYourEligibility.API.Boundary.Responses;

namespace CheckYourEligibility.API.UseCases;

public interface ICreateApplicationsFromBulkCheckUseCase
{
    Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds);
}

public class CreateApplicationsFromBulkCheckUseCase : ICreateApplicationsFromBulkCheckUseCase
{
    public async Task<MessageResponse> Execute(string bulkCheckId, List<int> allowedLocalAuthorityIds)
    {
        throw new NotImplementedException();
    }
}