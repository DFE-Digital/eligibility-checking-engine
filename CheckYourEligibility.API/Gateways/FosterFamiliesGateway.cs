using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;

public class FosterFamiliesGateway : IFosterFamilies
{
    private readonly IEligibilityCheckContext _db;

    private readonly IWorkingFamiliesEvent _workingFamiliesEventGateway;

    private ILogger _logger;

    public FosterFamiliesGateway(
        IEligibilityCheckContext db,
        IWorkingFamiliesEvent workingFamiliesEventGateway,
        ILogger<FosterFamiliesGateway> logger
    )
    {
        _db = db;
        _workingFamiliesEventGateway = workingFamiliesEventGateway;
        _logger = logger;
    }

    public async Task<FosterFamilyResponse> GetFosterFamily(
    Guid fosterCarerId,
    int localAuthorityId,
    bool includeChildren = false)
    {
        FosterFamilyResponse? result;

        if (includeChildren)
        {
            result = await _db.FosterCarers
                .Where(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId)
                .Select(x => new FosterFamilyResponse
                {
                    FosterCarerId = x.FosterCarerId,
                    CarerFirstName = x.FirstName,
                    CarerLastName = x.LastName,
                    CarerDateOfBirth = x.DateOfBirth,
                    CarerNationalInsuranceNumber = x.NationalInsuranceNumber,
                    HasPartner = x.HasPartner,
                    PartnerFirstName = x.PartnerFirstName,
                    PartnerLastName = x.PartnerLastName,
                    PartnerDateOfBirth = x.PartnerDateOfBirth,
                    PartnerNationalInsuranceNumber = x.PartnerNationalInsuranceNumber,
                    LocalAuthorityID = x.LocalAuthorityID,
                    FosterChildren = x.FosterChildren.Select(c =>
                        new FosterChildSummaryResponse
                        {
                            FosterChildId = c.FosterChildId,
                            FirstName = c.FirstName,
                            LastName = c.LastName,
                            DateOfBirth = c.DateOfBirth,
                            EligibilityCode = c.EligibilityCode,
                            Status = c.Status
                        })
                        .ToList()
                })
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }
        else
        {
            result = await _db.FosterCarers
                .Where(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId)
                .Select(x => new FosterFamilyResponse
                {
                    FosterCarerId = x.FosterCarerId,
                    CarerFirstName = x.FirstName,
                    CarerLastName = x.LastName,
                    CarerDateOfBirth = x.DateOfBirth,
                    CarerNationalInsuranceNumber = x.NationalInsuranceNumber,
                    HasPartner = x.HasPartner,
                    PartnerFirstName = x.PartnerFirstName,
                    PartnerLastName = x.PartnerLastName,
                    PartnerDateOfBirth = x.PartnerDateOfBirth,
                    PartnerNationalInsuranceNumber = x.PartnerNationalInsuranceNumber
                })
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        if (result is null)
        {
            _logger.LogWarning("Foster carer with ID {FosterCarerId} not found", fosterCarerId);
            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }

        return result;
    }

    public async Task<FosterCarer> GetFosterCarer(Guid fosterCarerId, int localAuthorityId)
    {
        FosterCarer? result = await _db.FosterCarers
            .Where(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId)
            .AsNoTracking()
            .SingleOrDefaultAsync();

        if (result is null)
        {
            _logger.LogWarning("Foster carer with ID {FosterCarerId} not found", fosterCarerId);
            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }
        return result;
    }

    public async Task<FosterFamilyCreatedResponse> CreateFosterFamily(
    FosterFamilyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool existingFosterFamily = await _db.FosterCarers
        .AnyAsync(x =>
        x.NationalInsuranceNumber == request.FosterCarer.CarerNationalInsuranceNumber &&
        x.LocalAuthorityID == request.FosterCarer.LocalAuthorityID);

        if (existingFosterFamily)
        {
            throw new ValidationException(
                null,
                $"A foster family with National Insurance number '{request.FosterCarer.CarerNationalInsuranceNumber}' already exists."
            );
        }

        string eligibilityCode = await GetEligibilityCodeForFosterChild();

        var fosterCarer = FosterFamiliesHelper.BuildFosterCarer(request.FosterCarer, request.Partner, request.HasPartner);
        var fosterChild = FosterFamiliesHelper.BuildFosterChild(request.FosterChild, request.SubmissionDate, fosterCarer.FosterCarerId);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var workingEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyEventFromFosterFamily(
                fosterCarer,
                fosterChild,
                eligibilityCode,
                request.SubmissionDate
            );
            var newWorkingSummaryEvent = WorkingFamiliesEventHelper.MapWorkingFamiliesEventToNewSummaryRecord(workingEvent);

            fosterChild.EligibilityCode = eligibilityCode;

            // Obsolete fields only set for compatibility
            fosterChild.ValidityStartDate = newWorkingSummaryEvent.ValidityStartDate;
            fosterChild.ValidityEndDate = newWorkingSummaryEvent.ValidityEndDate;

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent);
            await _db.WorkingFamiliesEventSummaries.AddAsync(newWorkingSummaryEvent);

            fosterChild.EligibilityCode = workingEvent.EligibilityCode;
            fosterChild.WorkingFamiliesEventSummaryID = newWorkingSummaryEvent.WorkingFamiliesEventSummaryID;

            await _db.FosterCarers.AddAsync(fosterCarer);
            await _db.FosterChildren.AddAsync(fosterChild);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            ReconfirmationProperties reconfirmation = WorkingFamiliesCheckHelper
            .SetReconfirmationProperties(
                workingEvent.ValidityEndDate.ToString(),
                workingEvent.GracePeriodEndDate.ToString(),
                request.SubmissionDate,
                EligibilityCodeType.Foster,
                request.FosterChild.ChildDateOfBirth.ToString()
            );

            return new FosterFamilyCreatedResponse()
            {
                FosterCarerId = fosterCarer.FosterCarerId,
                FosterChildId = fosterChild.FosterChildId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster family");
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateFosterCarer(Guid fosterCarerId, int localAuthorityId, UpdateFosterCarerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);

        if (fosterCarer is null)
        {
            _logger.LogWarning("Foster carer with ID {FosterCarerId} not found", fosterCarerId);

            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }

        if (request.FosterCarerRequest is not null)
        {
            fosterCarer.FirstName = request.FosterCarerRequest.CarerFirstName;
            fosterCarer.LastName = request.FosterCarerRequest.CarerLastName;
            fosterCarer.DateOfBirth = request.FosterCarerRequest.CarerDateOfBirth;
            fosterCarer.NationalInsuranceNumber = request.FosterCarerRequest.CarerNationalInsuranceNumber;
            fosterCarer.HasPartner = request.FosterCarerRequest.HasPartner;
            if (!fosterCarer.HasPartner)
            {
                // Clear partner fields if update if no partner is specified
                fosterCarer.PartnerFirstName = null;
                fosterCarer.PartnerLastName = null;
                fosterCarer.PartnerDateOfBirth = null;
                fosterCarer.PartnerNationalInsuranceNumber = null;
            }
        }

        if (request.FosterPartnerRequest is not null)
        {
            fosterCarer.HasPartner = true;
            fosterCarer.PartnerFirstName = request.FosterPartnerRequest.PartnerFirstName;
            fosterCarer.PartnerLastName = request.FosterPartnerRequest.PartnerLastName;
            fosterCarer.PartnerDateOfBirth = request.FosterPartnerRequest.PartnerDateOfBirth;
            fosterCarer.PartnerNationalInsuranceNumber = request.FosterPartnerRequest.PartnerNationalInsuranceNumber;
        }

        fosterCarer.Updated = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteFosterCarer(Guid fosterCarerId, int localAuthorityId)
    {
        var fosterCarer = await _db.FosterCarers
            .Include(x => x.FosterChildren)
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);


        if (fosterCarer is null)
        {
            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }

        _db.FosterChildren.RemoveRange(fosterCarer.FosterChildren);
        _db.FosterCarers.Remove(fosterCarer);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteFosterPartner(Guid fosterCarerId, int localAuthorityId)
    {
        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);

        if (fosterCarer is null)
        {
            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }

        fosterCarer.HasPartner = false;

        fosterCarer.PartnerFirstName = null;
        fosterCarer.PartnerLastName = null;
        fosterCarer.PartnerDateOfBirth = null;
        fosterCarer.PartnerNationalInsuranceNumber = null;

        fosterCarer.Updated = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<FosterFamiliesSearchResponse> SearchFosterFamilies(int localAuthorityId, FosterFamiliesSearchRequest request)
    {
        const int defaultPageSize = 10;

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? defaultPageSize : request.PageSize;

        // Construct base query including optional NINO filter
        var baseQuery = _db.FosterChildren
            .Include(x => x.FosterCarer)
            .Where(x => x.FosterCarer.LocalAuthorityID == localAuthorityId);

        if (!string.IsNullOrWhiteSpace(request.NINOFilter))
        {
            var ninoFilter = request.NINOFilter.Trim();
            baseQuery = baseQuery.Where(x =>
                x.FosterCarer.NationalInsuranceNumber == ninoFilter ||
                x.FosterCarer.PartnerNationalInsuranceNumber == ninoFilter);
        }

        // Calculate record count and max pages
        var totalRecords = await baseQuery.CountAsync();
        var maxPage = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)pageSize);
        if (pageNumber > maxPage) { pageNumber = maxPage; }

        // Generate result set
        var results = await baseQuery
            .OrderByDescending(x => x.SubmissionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.WorkingFamiliesEventSummary)
            .Select(x => new FosterFamiliesSearchItemResponse
            {
                FosterCarerId = x.FosterCarerId,
                FosterChildId = x.FosterChildId,
                ChildName = $"{x.FirstName} {x.LastName}",
                ChildDateOfBirth = x.DateOfBirth,
                EligibilityCode = x.EligibilityCode,
                CarerName = $"{x.FosterCarer.FirstName} {x.FosterCarer.LastName}",
                ValidityStartDate = x.WorkingFamiliesEventSummary.ValidityStartDate,
                ValidityEndDate = x.WorkingFamiliesEventSummary.ValidityEndDate,
                GracePeriodEndDate = x.WorkingFamiliesEventSummary.GracePeriodEndDate.Value
            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var item in results)
        {
            item.ReconfirmationProperties = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
                item.ValidityEndDate.ToString(),
                item.GracePeriodEndDate.ToString(),
                GetCheckDate(),
                EligibilityCodeType.Foster,
                item.ChildDateOfBirth.ToString()
            );
        }

        return new FosterFamiliesSearchResponse
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalNumberOfRecords = totalRecords,
            Data = results
        };
    }

    public async Task<FosterChildResponse> GetFosterChild(
    Guid fosterChildId,
    int localAuthorityId,
    bool includeFosterCarer = false)
    {
        FosterChildResponse? result;

        result = await _db.FosterChildren
            .Where(x =>
                x.FosterChildId == fosterChildId &&
                x.FosterCarer.LocalAuthorityID == localAuthorityId)
            .Include(x => x.WorkingFamiliesEventSummary)
            .Select(x => new FosterChildResponse
            {
                FosterChildId = x.FosterChildId,
                EligibilityCode = x.EligibilityCode,
                ChildFirstName = x.FirstName,
                ChildLastName = x.LastName,
                ChildFullName = $"{x.FirstName} {x.LastName}",
                ChildDateOfBirth = x.DateOfBirth,
                ChildPostCode = x.PostCode,
                PostCode = x.PostCode,
                FosterCarerId = x.FosterCarerId,
                CarerName = includeFosterCarer ? $"{x.FosterCarer.FirstName} {x.FosterCarer.LastName}" : null,
                PartnerName = includeFosterCarer && x.FosterCarer.HasPartner
                    ? $"{x.FosterCarer.PartnerFirstName} {x.FosterCarer.PartnerLastName}"
                    : null,
                ValidityStartDate = x.WorkingFamiliesEventSummary.ValidityStartDate,
                ValidityEndDate = x.WorkingFamiliesEventSummary.ValidityEndDate,
                GracePeriodEndDate = x.WorkingFamiliesEventSummary.GracePeriodEndDate.Value
            })
            .AsNoTracking()
            .SingleOrDefaultAsync();

        if (result is null)
        {
            _logger.LogWarning("Foster child with ID {FosterChildId} not found", fosterChildId);
            throw new NotFoundException($"Foster child {fosterChildId} not found");
        }

        var checkDate = GetCheckDate();

        // Calculate child too young
        result.ChildTooYoung = WorkingFamiliesCheckHelper.ChildIsTooYoung(
            result.ChildDateOfBirth,
            checkDate
        );

        // Calculate term validity
        result.TermValidity = WorkingFamiliesCheckHelper.SetTermValidity(
            checkDate,
            result.GracePeriodEndDate.ToString(),
            result.ValidityStartDate.ToString(),
            result.ChildDateOfBirth.ToString()
        );

        // Calculate reconfirmation properties
        result.ReconfirmationProperties = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
            result.ValidityEndDate.ToString(),
            result.GracePeriodEndDate.ToString(),
            checkDate,
            EligibilityCodeType.Foster,
            result.ChildDateOfBirth.ToString()
        );

        return result;
    }

    protected virtual DateTime GetCheckDate() => DateTime.Today;

    public async Task<FosterChildResponse> CreateFosterChild(
    FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get existing carer
        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);

        if (fosterCarer is null)
        {
            throw new NotFoundException($"Foster carer {fosterCarerId} not found");
        }

        string eligibilityCode = await GetEligibilityCodeForFosterChild();

        // build domain model from request
        var fosterChild = FosterFamiliesHelper.BuildFosterChild(request, DateTime.UtcNow, fosterCarerId);

        // link child to current foster carer
        fosterChild.FosterCarerId = fosterCarer.FosterCarerId;
        fosterChild.EligibilityCode = eligibilityCode;

        // Create new wf event
        var workingEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyEventFromFosterFamily(
             fosterCarer,
             fosterChild,
             eligibilityCode,
             submissionDate
        );
        var newWorkingSummaryEvent = WorkingFamiliesEventHelper.MapWorkingFamiliesEventToNewSummaryRecord(workingEvent);
        fosterChild.EligibilityCode = workingEvent.EligibilityCode;
        fosterChild.WorkingFamiliesEventSummaryID = newWorkingSummaryEvent.WorkingFamiliesEventSummaryID;

        // Obsolete fields only set for compatibility
        fosterChild.ValidityStartDate = newWorkingSummaryEvent.ValidityStartDate;
        fosterChild.ValidityEndDate = newWorkingSummaryEvent.ValidityEndDate;

        await _db.WorkingFamiliesEvents.AddAsync(workingEvent);
        await _db.WorkingFamiliesEventSummaries.AddAsync(newWorkingSummaryEvent);
        await _db.FosterChildren.AddAsync(fosterChild);
        await _db.SaveChangesAsync();

        // Return the response as a get for the new record
        return await GetFosterChild(fosterChild.FosterChildId, localAuthorityId, true);
    }

    public async Task<FosterChildResponse> UpdateFosterChild(
    Guid fosterChildId,
    int localAuthorityId,
    UpdateFosterChildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fosterChild = await _db.FosterChildren
            .Include(x => x.FosterCarer)
            .SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId && x.FosterCarer.LocalAuthorityID == localAuthorityId);
        if (fosterChild is null) { throw new NotFoundException($"Foster child {fosterChildId} not found"); }

        fosterChild.FirstName = request.FosterChildRequest.ChildFirstName;
        fosterChild.LastName = request.FosterChildRequest.ChildLastName;
        fosterChild.DateOfBirth = request.FosterChildRequest.ChildDateOfBirth;
        fosterChild.PostCode = request.FosterChildRequest.ChildPostCode;
        fosterChild.Updated = DateTime.UtcNow;

        // TODO - Update Working Family Events / summary here

        await _db.SaveChangesAsync();

        return await GetFosterChild(fosterChildId, fosterChild.FosterCarer.LocalAuthorityID.Value, true);
    }

    public async Task<FosterChildResponse> ReconfirmFosterChild(Guid fosterChildId, int localAuthorityId, DateTime submissionDate)
    {
        // Get foster child record
        var fosterChild = await _db.FosterChildren.Include(x => x.FosterCarer).SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId);
        if (fosterChild is null) { throw new NotFoundException($"Foster child {fosterChildId} not found"); }

        // Validate LA for foster carer record
        if (fosterChild.FosterCarer.LocalAuthorityID != localAuthorityId)
        {
            throw new NotFoundException($"Foster child is not associated with selected local authority");
        }

        // Create new wf event
        var newEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyEventFromFosterFamily(
            fosterChild.FosterCarer,
            fosterChild,
            fosterChild.EligibilityCode,
            submissionDate
        );
        await _db.WorkingFamiliesEvents.AddAsync(newEvent);

        // Update summary record
        var existingSummaryRecord = await _workingFamiliesEventGateway.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode);
        int historicEventRecordsCount = await _workingFamiliesEventGateway.GetWorkingFamiliesEventsCount(fosterChild.EligibilityCode);
        existingSummaryRecord = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(newEvent, existingSummaryRecord, historicEventRecordsCount);

        // Obsolete fields only set for compatibility
        fosterChild.ValidityStartDate = existingSummaryRecord.ValidityStartDate;
        fosterChild.ValidityEndDate = existingSummaryRecord.ValidityEndDate;

        // Save all changes
        await _db.SaveChangesAsync();

        // Return the response as a get for the new record
        return await GetFosterChild(fosterChild.FosterChildId, localAuthorityId, true);
    }


    public async Task<FosterCodePreviewResponse> PreviewReconfirmFosterChild(Guid fosterChildId, int localAuthorityId, DateTime submissionDate)
    {
        // Get foster child record
        var fosterChild = await _db.FosterChildren.Include(x => x.FosterCarer).SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId);
        if (fosterChild is null) { throw new NotFoundException($"Foster child {fosterChildId} not found"); }

        // Validate LA for foster carer record
        if (fosterChild.FosterCarer.LocalAuthorityID != localAuthorityId)
        {
            throw new NotFoundException($"Foster child is not associated with selected local authority");
        }

        // Create new wf event
        var newEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyEventFromFosterFamily(
            fosterChild.FosterCarer,
            fosterChild,
            fosterChild.EligibilityCode,
            submissionDate
        );

        // Create updated summary record
        var existingSummaryRecord = await _workingFamiliesEventGateway.GetWorkingFamiliesEventSummaryRecordByEligibilityCode(fosterChild.EligibilityCode);
        int historicEventRecordsCount = await _workingFamiliesEventGateway.GetWorkingFamiliesEventsCount(fosterChild.EligibilityCode);
        existingSummaryRecord = WorkingFamiliesEventHelper.EvaluateContiguityForCodeFromIncomingEvent(newEvent, existingSummaryRecord, historicEventRecordsCount);

        // Term validity
        var termValidity = WorkingFamiliesCheckHelper.SetTermValidity(
            DateTime.Today,
            existingSummaryRecord.GracePeriodEndDate.ToString(),
            existingSummaryRecord.ValidityStartDate.ToString(),
            fosterChild.DateOfBirth.ToString());

        // Reconfirmation properties       
        var reconfirmation = WorkingFamiliesCheckHelper.SetReconfirmationProperties(
            existingSummaryRecord.ValidityEndDate.ToString(),
            existingSummaryRecord.GracePeriodEndDate.ToString(),
            submissionDate,
            EligibilityCodeType.Foster,
            fosterChild.DateOfBirth.ToString());

        // Generate response
        FosterCodePreviewResponse response = new()
        {
            ValidityStartDate = existingSummaryRecord.ValidityStartDate,
            ValidFromTerm = termValidity.Current.Name != TermName.None ? termValidity.Current : termValidity.Next,
            ReconfirmBetweenStart = reconfirmation.StartDate,
            ReconfirmBetweenEnd = reconfirmation.EndDate,
            GracePeriodEndDate = existingSummaryRecord.GracePeriodEndDate.Value
        };
        return response;
    }



    public async Task DeleteFosterChild(Guid fosterChildId, int localAuthorityId)
    {
        var fosterChild = await _db.FosterChildren
            .Include(x => x.FosterCarer)
            .SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId && x.FosterCarer.LocalAuthorityID == localAuthorityId);

        if (fosterChild is null)
        {
            _logger.LogWarning(
                "Foster child with ID {FosterChildId} not found",
                fosterChildId);

            throw new NotFoundException(
                $"Foster child {fosterChildId} not found");
        }

        _db.FosterChildren.Remove(fosterChild);

        await _db.SaveChangesAsync();
    }

    #region helpers


    public async Task<string> GetEligibilityCodeForFosterChild()
    {
        const EligibilityCodeType rangeName = EligibilityCodeType.Foster;

        // Existing fast unit tests use EF's InMemory provider, which cannot
        // execute SQL Server-specific commands.
        if (!_db.Database.IsSqlServer())
        {
            return await GetEligibilityCodeForNonSqlServerProvider(rangeName);
        }

        var connection = _db.Database.GetDbConnection();
        var shouldCloseConnection =
            connection.State != ConnectionState.Open;

        try
        {
            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();

            var rangeNameParameter = command.CreateParameter();
            rangeNameParameter.ParameterName = "@rangeName";
            rangeNameParameter.Value = rangeName.ToString();
            command.Parameters.Add(rangeNameParameter);

            command.CommandText =
                """
            SET NOCOUNT ON;

            UPDATE [EligibilityCodeRanges]
            SET [NextAvailableCode] = [NextAvailableCode] + 1
            OUTPUT DELETED.[NextAvailableCode]
            WHERE [Name] = @rangeName
              AND [NextAvailableCode] <= [EndRange];
            """;

            var result = await command.ExecuteScalarAsync();

            if (result is null || result is DBNull)
            {
                throw new InvalidOperationException(
                    "Eligibility Code unavailable.");
            }

            return Convert
                .ToInt64(result, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<string> GetEligibilityCodeForNonSqlServerProvider(
        EligibilityCodeType rangeName)
    {
        var range = await _db.EligibilityCodeRanges
            .SingleAsync(x => x.Name == rangeName);

        if (range.NextAvailableCode > range.EndRange)
        {
            throw new InvalidOperationException(
                "Eligibility Code unavailable.");
        }

        var code = range.NextAvailableCode;
        range.NextAvailableCode++;

        await _db.SaveChangesAsync();

        return code.ToString(CultureInfo.InvariantCulture);
    }

    #endregion
}
