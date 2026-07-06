using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

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
    /// <summary>
    /// Returns report by ID
    /// </summary>
    /// <param name="reportId"></param>
    /// <returns>Report record</returns>
    public async Task<EligibilityCheckReport?> GetEligibilityReportById(Guid reportId)
    {
        var report = await _db.EligibilityCheckReports.FirstOrDefaultAsync(r => r.EligibilityCheckReportId == reportId);
        return report;
    }

    public async Task<Dictionary<Guid, EligibilityCheck>> GetEligibilityChecksByReportId(Guid reportId)
    {
        if (await GetEligibilityReportById(reportId) == null)
        {
            _logger.LogWarning($"Eligibility check report with ID {reportId} not found");
            throw new NotFoundException();
        }

        var reportItems = await _db.EligibilityCheckReportItem
            .Where(i => i.EligibilityCheckReportId == reportId)
            .Include(x => x.EligibilityCheck)
            .Select(x => x.EligibilityCheck)
            .ToListAsync();

        if (!reportItems.Any())
            return new Dictionary<Guid, EligibilityCheck>();

        return reportItems.ToDictionary(
             c => Guid.Parse(c.EligibilityCheckID),
             c => c
        );
    }

    /// <summary>
    /// Backend process for report generation
    /// </summary>
    /// <param name="reportId"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="eligiblityCheckType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task EligibilityCheckReports(
    Guid reportId,
    CheckEligibilityType eligiblityCheckType,
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
        const int BatchSize = 1000;
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

                // filter by check type to match index on EligibilityCheck table
                // order by check id to ensure we get the next batch of checks
                // take the next batch of checks
                // select the check id and whether it is a bulk check
                var checks = await query
                    .Where(c => c.Type == eligiblityCheckType)
                    .OrderBy(e => e.EligibilityCheckID)
                    .Take(BatchSize)
                    .Select(e => new CheckResult(
                        e.EligibilityCheckID,
                        e.BulkCheckID != null)).ToListAsync(cancellationToken);

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

    public async Task<EligibilityCheckReportHistoryResponse> GetEligibilityCheckReportHistory(string localAuthorityId, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(localAuthorityId))
            throw new ArgumentNullException(nameof(localAuthorityId));

        const int pageSize = 10;
        var localAuthorityIntId = int.Parse(localAuthorityId);

        try
        {
            // Base query to get non-deleted reports for the specified local authority
            var query = _db.EligibilityCheckReports
                .Where(r => !r.IsDeleted && r.LocalAuthorityID == localAuthorityIntId);

            // Get total records
            var totalRecords = await query.CountAsync();

            // Ensure page number is valid
            pageNumber = pageNumber < 1 ? 1 : pageNumber;

            // Calculate total pages and adjust page number if it exceeds max
            var maxPage = totalRecords == 0
                ? 1
                : (int)Math.Ceiling(totalRecords / (double)pageSize);

            // If requested page number exceeds max, return the last page
            if (pageNumber > maxPage)
            {
                pageNumber = maxPage;
            }

            // Retrieve paginated results
            var reportHistoryItems = await query
                .OrderByDescending(r => r.ReportGeneratedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new EligibilityCheckReportHistoryItem
                {
                    ReportID = r.EligibilityCheckReportId.ToString(),
                    ReportGeneratedDate = r.ReportGeneratedDate,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    GeneratedBy = r.GeneratedBy,
                    NumberOfResults = r.NumberOfResults,
                    Status = r.Status.ToString()
                })
                .ToListAsync();

            return new EligibilityCheckReportHistoryResponse
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalNumberOfRecords = totalRecords,
                Data = reportHistoryItems,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving eligibility check report history");
            throw;
        }
    }

    public async Task<int> GetLocalAuthorityIdForReport(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty)
            throw new ArgumentNullException(nameof(reportId));

        var report = await _db.EligibilityCheckReports
            .FirstOrDefaultAsync(r => r.EligibilityCheckReportId == reportId && !r.IsDeleted, cancellationToken);

        if (report is null)
            throw new NotFoundException("Eligibility report not found");

        if (!report.LocalAuthorityID.HasValue)
            throw new InvalidOperationException("Eligibility report does not have a local authority ID");

        return report.LocalAuthorityID.Value;
    }

    public async Task DeleteEligibilityCheckReport(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty)
            throw new ArgumentNullException(nameof(reportId));

        var report = await _db.EligibilityCheckReports
            .FirstOrDefaultAsync(r => r.EligibilityCheckReportId == reportId && !r.IsDeleted, cancellationToken);

        if (report is null)
            throw new NotFoundException("Eligibility report not found");

        report.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    #region helpers

    private sealed record CheckResult(
        string EligibilityCheckID,
        bool IsBulk
    );

    // public to allow testing
    public IQueryable<EligibilityCheck> GetCheckQuery(EligibilityCheckReport request)
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