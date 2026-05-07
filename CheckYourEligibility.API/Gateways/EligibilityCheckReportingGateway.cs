using CheckYourEligibility.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public sealed class EligibilityCheckReportingGateway : IEligibilityCheckReporting
{
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger<EligibilityCheckReportingGateway> _logger;

    public EligibilityCheckReportingGateway(
        IEligibilityCheckContext db,
        ILogger<EligibilityCheckReportingGateway> logger)
    {
        _db = db;
        _logger = logger;
    }


    public async Task EligibilityCheckReports(
    Guid reportId,
    CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty)
            throw new ArgumentNullException(nameof(reportId));

        var report = await _db.EligibilityCheckReports
        .FirstOrDefaultAsync(r => r.EligibilityCheckReportId == reportId, cancellationToken);

        if (report is null)
            throw new InvalidOperationException("Report not found");

        // set the report status to generating
        report.Status = ReportStatus.Generating;
        await _db.SaveChangesAsync(cancellationToken);

        // they could be a lot of checks, so we need to batch the inserts
        // to avoid loading them all into memory at once
        const int BatchSize = 10_000;
        var batch = new List<EligibilityCheckReportItem>(BatchSize);
        var totalResults = 0;
        string? lastProcessedCheckId = null;

        try
        {
            while (true)
            {
                var query = GetCheckQuery(report);

                // only apply filter after first batch
                if (lastProcessedCheckId != null)
                {
                    query = query.Where(c => string.Compare(c.EligibilityCheckID, lastProcessedCheckId) > 0);
                }

                var checks = await query
                        .OrderBy(e => e.EligibilityCheckID)
                        .Take(BatchSize)
                        .Select(e => new CheckResult(
                            e.EligibilityCheckID,
                            e.BulkCheck != null ))
                        .ToListAsync(cancellationToken);

                // if no more checks, break the loop
                if (checks.Count == 0)
                    break;

                // add the checks to the batch
                batch.AddRange(checks.Select(check => new EligibilityCheckReportItem
                {
                    EligibilityCheckReportId = report.EligibilityCheckReportId,
                    EligibilityCheckID = check.EligibilityCheckID,
                    IsBulkCheckItem = check.IsBulk
                }));

                // insert the batch into the database
                _db.BulkInsert_EligibilityCheckReportItems(batch);
                batch.Clear();

                // update the total results count
                totalResults += checks.Count;

                // update the last processed check id
                lastProcessedCheckId = checks[^1].EligibilityCheckID;
            }

            report.Status = ReportStatus.Complete;
            report.NumberOfResults = totalResults;
            await _db.SaveChangesAsync(cancellationToken);

        }
        catch (Exception ex)
        {
            report.Status = ReportStatus.Failed;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Error generating eligibility check report");
            throw;
        }
    }

    public async Task<EligibilityCheckReport> CreateReport(EligibilityCheckReportRequest request, CancellationToken cancellationToken)
    {
        var report = new EligibilityCheckReport
        {
            EligibilityCheckReportId = Guid.NewGuid(),
            LocalAuthorityID = request.LocalAuthorityID,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfResults = 0,
            GeneratedBy = request.GeneratedBy,
            CheckType = request.CheckType,
            Status = ReportStatus.New
        };

        _db.EligibilityCheckReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        return report;
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

    #region private helpers

    private sealed record CheckResult(
        string EligibilityCheckID,
        bool IsBulk
    );

    private IQueryable<EligibilityCheck> GetCheckQuery(EligibilityCheckReport request)
    {
        return request.CheckType switch
        {
            CheckType.BulkChecks =>
                _db.BulkChecks
                    .Where(b =>
                        b.LocalAuthorityID == request.LocalAuthorityID &&
                        b.SubmittedDate >= request.StartDate &&
                        b.SubmittedDate <= request.EndDate)
                    .SelectMany(b => b.EligibilityChecks!)
                    .AsNoTracking(),

            CheckType.IndividualChecks =>
                _db.CheckEligibilities
                    .Where(c =>
                        c.BulkCheck == null &&
                        c.OrganisationID == request.LocalAuthorityID &&
                        c.Created >= request.StartDate &&
                        c.Created <= request.EndDate)
                    .AsNoTracking(),

            CheckType.AllChecks =>
                _db.CheckEligibilities
                    .Where(c =>
                        c.OrganisationID == request.LocalAuthorityID &&
                        c.Created >= request.StartDate &&
                        c.Created <= request.EndDate)
                    .AsNoTracking(),

            _ => throw new ArgumentOutOfRangeException(nameof(request.CheckType))
        };
    }


    #endregion
}