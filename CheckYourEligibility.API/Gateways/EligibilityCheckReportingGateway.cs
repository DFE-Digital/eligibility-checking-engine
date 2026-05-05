using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Gateways;

public class EligibilityCheckReportingGateway : IEligibilityCheckReporting
{
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger<EligibilityCheckReportingGateway> _logger;

    public EligibilityCheckReportingGateway(
        IConfiguration configuration,
        IEligibilityCheckContext dbContext,
        ILogger<EligibilityCheckReportingGateway> logger
    )
    {
        _db = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IEnumerable<EligibilityCheckReportResponseItem>> EligibilityCheckReports(EligibilityCheckReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Get bulk checks between the specified dates for the given local authority
            var bulkChecks = await _db.BulkChecks.Where(x => x.LocalAuthorityID == request.LocalAuthorityID &&
                                                       x.SubmittedDate >= request.StartDate &&
                                                       x.SubmittedDate <= request.EndDate)
                                                       .Include(ec => ec.EligibilityChecks)
                                                       .ToListAsync();

            if (bulkChecks == null || bulkChecks.Count == 0)
            {
                _logger.LogInformation($"No bulk checks found for LocalAuthorityID: {request.LocalAuthorityID} between {request.StartDate} and {request.EndDate}");
                throw new Exception($"No bulk checks found");
            }

            // Flatten the eligibility checks and deserialize the CheckData into EligibilityCheckReportResponseItem, while also adding the CheckedBy and DateCheckSubmitted fields
            var reportItems = bulkChecks
                .Where(bulkCheck => bulkCheck?.EligibilityChecks != null)
                .SelectMany(bulkCheck => bulkCheck.EligibilityChecks
                    .Where(check => !string.IsNullOrWhiteSpace(check?.CheckData))
                    .Select(check =>
                    {
                        EligibilityCheckReportResponseItem? parsedCheck = null;
                        try
                        {
                            parsedCheck = JsonConvert.DeserializeObject<EligibilityCheckReportResponseItem>(check.CheckData);
                            parsedCheck.Outcome = check.Status;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to deserialize EligibilityCheckReportResponseItem for BulkCheckID: {bulkCheck.BulkCheckID}");
                            return null;
                        }
                        if (parsedCheck == null) return null;
                        parsedCheck.CheckedBy = bulkCheck.SubmittedBy ?? string.Empty;
                        parsedCheck.DateCheckSubmitted = bulkCheck.SubmittedDate;
                        return parsedCheck;
                    })
                )
                .Where(item => item != null)
                .ToList();


            if (request.SaveRequestAudit)
            {
                // Save audit of report generated in EligibilityCheckReportRequests
                var reportAudit = new EligibilityCheckReport
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    GeneratedBy = request.GeneratedBy,
                    LocalAuthorityID = request.LocalAuthorityID,
                    NumberOfResults = reportItems.Count
                };

                await _db.EligibilityCheckReports.AddAsync(reportAudit);
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync(cancellationToken);
            return reportItems;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error generating bulk check report");
            throw new Exception($"Error generating bulk check report: {ex.Message}");
        }
    }

    public async Task<IEnumerable<EligibilityCheckReportHistoryItem>> GetEligibilityCheckReportHistory(string localAuthorityId)
    {
        if (string.IsNullOrEmpty(localAuthorityId))
            throw new ArgumentNullException(nameof(localAuthorityId));

        try
        {
            var reportHistory = await _db.EligibilityCheckReports
                .Where(x => x.ReportGeneratedDate > DateTime.UtcNow.AddDays(-7))
                .Where(r => r.LocalAuthorityID == int.Parse(localAuthorityId))
                .OrderByDescending(r => r.ReportGeneratedDate)
                .Select(r => new EligibilityCheckReportHistoryItem
                {
                    ReportGeneratedDate = r.ReportGeneratedDate,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    GeneratedBy = r.GeneratedBy,
                    NumberOfResults = r.NumberOfResults
                })
                .ToListAsync();

            return reportHistory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving eligibility check report history");
            throw new Exception($"Error retrieving eligibility check report history: {ex.Message}");
        }
    }
}