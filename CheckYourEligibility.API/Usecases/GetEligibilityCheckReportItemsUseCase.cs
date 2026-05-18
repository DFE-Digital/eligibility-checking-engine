using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Usecases
{
    public interface IGetEligibilityCheckReportItemsUseCase
    {
        Task<EligibilityCheckReportItemsResponse> Execute(string reportId);
    }

    public class GetEligibilityCheckReportItemsUseCase : IGetEligibilityCheckReportItemsUseCase
    {
        private readonly IEligibilityCheckReporting _eligibilityCheckReportingGateway;
        private readonly ILogger<GetEligibilityCheckReportItemsUseCase> _logger;

        public GetEligibilityCheckReportItemsUseCase(IEligibilityCheckReporting eligibilityCheckReportingGateway, ILogger<GetEligibilityCheckReportItemsUseCase> logger)
        {
            _eligibilityCheckReportingGateway = eligibilityCheckReportingGateway;
            _logger = logger;
        }

        public async Task<EligibilityCheckReportItemsResponse> Execute(string reportId)
        {
            if (!Guid.TryParse(reportId, out Guid id))
                throw new ValidationException(null, "Invalid report ID format. Must be a GUID");

            if (await _eligibilityCheckReportingGateway.GetEligibilityReportById(id) == null)
            {
                _logger.LogWarning($"Eligibility check report with ID {reportId} not found");
                throw new NotFoundException();
            }

            var checksDict = await _eligibilityCheckReportingGateway
                .GetEligibilityChecksByReportId(id);

            if (!checksDict.Any())
            {

                return new EligibilityCheckReportItemsResponse
                {
                    Data = []
                };

            }

            var itemsList = checksDict
                .AsParallel()
                .Select(item =>
                {
                    var check = item.Value;
                    var checkData = JsonConvert.DeserializeObject<CheckProcessData>(check.CheckData);

                    return new CheckItem
                    {
                        ParentName = checkData.LastName ?? "",
                        NationalInsuranceNumber = checkData.NationalInsuranceNumber ?? "",
                        DateOfBirth = checkData.DateOfBirth,
                        CheckSubmittedDate = check.Created.ToString("yyyy-MM-dd"),
                        Outcome = check.Status.ToString(),
                        Tier = check.Tier?.ToString(),
                        CheckType = check.Type.ToString(),
                        CheckedBy = check.UserName ?? ""
                    };
                })
                .Where(x => x != null)
                .ToList();

            return new EligibilityCheckReportItemsResponse
            {
                Data = itemsList
            };
        }
    }
}