using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Exceptions;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.UseCases
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
                _logger.LogWarning("Eligibility check report with ID {ReportId} not found", id);
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
                        ProcessingType = check.BulkCheckID != null ? "Batch" : "Individual",
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