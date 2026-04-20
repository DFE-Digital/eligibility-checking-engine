using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

public class EligibilityReportingGateway : IEligibilityReporting
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly IEligibilityCheckContext _db;

    public EligibilityReportingGateway(
        IConfiguration configuration,
        ILogger<EligibilityReportingGateway> logger,
        IEligibilityCheckContext db
    )
    {
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    public async Task<EligibilityCheckReportResponse> CreateEligibilityCheckReport(
    EligibilityCheckReportRequest request,
    CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        // determine if there are results before we persist the report 
        // if more then one record, valid report, if zero records, don't create report and return error
        var query = BuildQuery(request);
        if (!await query.Take(1).AnyAsync(ct))
            throw new Exception("No results");

        var sql = query.ToQueryString();

        // only now do we persist
        var audit = new EligibilityCheckReport
        {
            LocalAuthorityID = request.LocalAuthorityID,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            GeneratedBy = request.GeneratedBy,
            NumberOfResults = await query.CountAsync(ct),
            CheckType = request.CheckType,
            ReportGeneratedDate = DateTime.UtcNow
        };

        _db.EligibilityCheckReports.Add(audit);
        await _db.SaveChangesAsync(ct);

        // return first page of results 
        return await GetEligibilityCheckReport(audit.EligibilityCheckReportId, request.LocalAuthorityID, 1, ct);
    }


    public async Task<EligibilityCheckReportResponse> GetEligibilityCheckReport(
    Guid reportId,
    int localAuthorityId,
    int pageNumber,
    CancellationToken cancellationToken = default)
    {
        const int maxPageNumber = 100;
        const int defaultPageSize = 100;

        if (pageNumber < 1 || pageNumber > maxPageNumber)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Page number must be between 1 and {maxPageNumber}");

        // Load the audit record (authoritative source)
        var reportAudit = await _db.EligibilityCheckReports
            .SingleOrDefaultAsync(r => r.EligibilityCheckReportId == reportId, cancellationToken);

        if (reportAudit == null)
            return null;

        if (reportAudit.LocalAuthorityID != localAuthorityId)
            throw new UnauthorizedAccessException("You do not have permission to view this report for the requested Local Authority");

        // Rebuild the query using persisted parameters
        var request = new EligibilityCheckReportRequest
        {
            LocalAuthorityID = (int)reportAudit.LocalAuthorityID,
            StartDate = reportAudit.StartDate,
            EndDate = reportAudit.EndDate,
            CheckType = reportAudit.CheckType,
            GeneratedBy = reportAudit.GeneratedBy
        };

        var query = BuildQuery(request);

        // Apply pagination
        var rawChecks = await query
            .Skip((pageNumber - 1) * defaultPageSize)
            .Take(defaultPageSize)
            .ToListAsync(cancellationToken);

        // Parse checks
        var reportItems = rawChecks
            .Select(ParseCheck)
            .Where(item => item != null)
            .ToList()!;

        // Calculate number of results per page
        var responsePageSize = defaultPageSize;
        if (reportItems.Count < defaultPageSize)
        {
            responsePageSize = reportItems.Count;
        }

        // Calculate total pages
        var totalPages = reportAudit.NumberOfResults == 0
            ? 0
            : (reportAudit.NumberOfResults + defaultPageSize - 1) / defaultPageSize;

        return new EligibilityCheckReportResponse
        {
            Data = reportItems,
            PageNumber = pageNumber,
            PageSize = responsePageSize,
            TotalCount = reportAudit.NumberOfResults,
            TotalPages = totalPages
        };
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


    #region private methods

    private sealed record RawCheck(
    string CheckData,
    CheckEligibilityStatus Status,
    string? CheckedBy,
    CheckType CheckType,
    DateTime SubmittedDate
    );

    private IQueryable<RawCheck> BuildQuery(
    EligibilityCheckReportRequest request)
{
    return request.CheckType switch
    {
        CheckType.BulkChecks =>
            _db.BulkChecks
                .Where(b =>
                    b.LocalAuthorityID == request.LocalAuthorityID &&
                    b.SubmittedDate >= request.StartDate &&
                    b.SubmittedDate <= request.EndDate)
                .Include(b => b.EligibilityChecks)
                .SelectMany(b => b.EligibilityChecks!
                    .Where(ec => !string.IsNullOrWhiteSpace(ec.CheckData))
                    .Select(c => new RawCheck(
                        c.CheckData,
                        c.Status,
                        b.SubmittedBy,
                        CheckType.BulkChecks, // always bulk
                        b.SubmittedDate
                    )))
                .AsNoTracking(),

        CheckType.IndividualChecks =>
            _db.CheckEligibilities
                .Where(c =>
                    c.BulkCheck == null &&
                    c.OrganisationID == request.LocalAuthorityID &&
                    c.Created >= request.StartDate &&
                    c.Created <= request.EndDate)
                .OrderBy(c => c.Created)
                .Select(c => new RawCheck(
                    c.CheckData,
                    c.Status,
                    null,
                    CheckType.IndividualChecks,
                    c.Created
                ))
                .AsNoTracking(),

        CheckType.AllChecks =>
            _db.CheckEligibilities
                .Where(c =>
                    c.OrganisationID == request.LocalAuthorityID &&
                    c.Created >= request.StartDate &&
                    c.Created <= request.EndDate)
                .OrderBy(c => c.Created)
                .Select(c => new RawCheck(
                    c.CheckData,
                    c.Status,
                    c.BulkCheck != null
                        ? c.BulkCheck.SubmittedBy
                        : null,
                    c.BulkCheck != null
                        ? CheckType.BulkChecks
                        : CheckType.IndividualChecks, // if bulk check exists, it's bulk, otherwise individual
                    c.Created
                ))
                .AsNoTracking(),

        _ => throw new ArgumentOutOfRangeException(nameof(request.CheckType))
    };
}
    private EligibilityCheckReportItem? ParseCheck(RawCheck raw)
    {
        if (string.IsNullOrWhiteSpace(raw.CheckData))
            return null;

        try
        {
            var parsed = JsonConvert.DeserializeObject<EligibilityCheckReportItem>(raw.CheckData);
            if (parsed == null)
                return null;

            parsed.Outcome = raw.Status;
            parsed.CheckedBy = raw.CheckedBy ?? string.Empty;
            parsed.DateCheckSubmitted = raw.SubmittedDate;
            parsed.CheckType = raw.CheckType;

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize EligibilityCheckReportItem from CheckData: {CheckData}", raw.CheckData);
            return null;
        }
    }

    #endregion

}





