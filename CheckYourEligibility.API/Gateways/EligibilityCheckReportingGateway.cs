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

    public async Task<IEnumerable<EligibilityCheckReportResponseItem>> EligibilityCheckReports(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty)
            throw new ArgumentNullException(nameof(reportId));

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // update the status
            var report = await _db.EligibilityCheckReports.FirstOrDefaultAsync(r => r.EligibilityCheckReportId == reportId, cancellationToken);
            report.Status = ReportStatus.Generating;
            await _db.SaveChangesAsync(cancellationToken);

            // get the checks and save them
            // to do

            // if successful update status again and reponse
            report.Status = ReportStatus.Complete;
            await _db.SaveChangesAsync(cancellationToken);


        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error generating bulk check report");
            throw new Exception($"Error generating bulk check report: {ex.Message}");
        }
    }

    internal async Task<Guid> SaveEligibilityCheckReportRequest(EligibilityCheckReportRequest request)
    {
        var reportAudit = new EligibilityCheckReport
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            GeneratedBy = request.GeneratedBy,
            LocalAuthorityID = request.LocalAuthorityID,
            Status = ReportStatus.New,
            NumberOfResults = 0
        };

        await _db.EligibilityCheckReports.AddAsync(reportAudit);
        await _db.SaveChangesAsync();

        return reportAudit.EligibilityCheckReportId;
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