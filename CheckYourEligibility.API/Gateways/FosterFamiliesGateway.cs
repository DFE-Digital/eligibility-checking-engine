using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using CheckYourEligibility.API.Helpers;
using CheckYourEligibility.API.Boundary.Responses.Internal;

public class FosterFamiliesGateway : IFosterFamilies
{
    private readonly IEligibilityCheckContext _db;
    private ILogger _logger;
    public FosterFamiliesGateway(IEligibilityCheckContext db, ILogger<FosterFamiliesGateway> logger)
    {
        _db = db;
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

        var fosterCarer = BuildFosterCarer(request.FosterCarer, request.Partner, request.HasPartner);
        var fosterChild = BuildFosterChild(request.FosterChild, request.SubmissionDate, fosterCarer.FosterCarerId);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var workingEvent =
               WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request, eligibilityCode);

            fosterChild.ValidityStartDate = workingEvent.ValidityStartDate;
            fosterChild.EligibilityCode = eligibilityCode;
            fosterChild.ValidityEndDate = workingEvent.ValidityEndDate;

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent);

            fosterChild.EligibilityCode = workingEvent.EligibilityCode;

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
                ChildName = $"{fosterChild.FirstName} {fosterChild.LastName}",
                EligibilityCode = workingEvent.EligibilityCode,
                Status = fosterChild.Status,
                EligibilityConfirmed = request.SubmissionDate,
                ReconfirmBetween = $"{reconfirmation.StartDate:dd MMMM yyyy} and {reconfirmation.EndDate:dd MMMM yyyy}",
                GracePeriodEndDate = workingEvent.GracePeriodEndDate,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster family");

            await transaction.RollbackAsync();

            throw;
        }
    }

    public async Task UpdateFosterCarer(
    Guid fosterCarerId,
    int localAuthorityId,
    UpdateFosterCarerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);

        if (fosterCarer is null)
        {
            _logger.LogWarning(
                "Foster carer with ID {FosterCarerId} not found",
                fosterCarerId);

            throw new NotFoundException(
                $"Foster carer {fosterCarerId} not found");
        }

        if (request.FosterCarerRequest is not null)
        {
            fosterCarer.FirstName =
                request.FosterCarerRequest.CarerFirstName;

            fosterCarer.LastName =
                request.FosterCarerRequest.CarerLastName;

            fosterCarer.DateOfBirth =
                request.FosterCarerRequest.CarerDateOfBirth;

            fosterCarer.NationalInsuranceNumber =
                request.FosterCarerRequest.CarerNationalInsuranceNumber;
        }

        if (request.FosterPartnerRequest is not null)
        {
            fosterCarer.HasPartner = true;

            fosterCarer.PartnerFirstName =
                request.FosterPartnerRequest.PartnerFirstName;

            fosterCarer.PartnerLastName =
                request.FosterPartnerRequest.PartnerLastName;

            fosterCarer.PartnerDateOfBirth =
                request.FosterPartnerRequest.PartnerDateOfBirth;

            fosterCarer.PartnerNationalInsuranceNumber =
                request.FosterPartnerRequest.PartnerNationalInsuranceNumber;
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
            throw new NotFoundException(
                $"Foster carer {fosterCarerId} not found");
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
            throw new NotFoundException(
                $"Foster carer {fosterCarerId} not found");
        }

        fosterCarer.HasPartner = false;

        fosterCarer.PartnerFirstName = null;
        fosterCarer.PartnerLastName = null;
        fosterCarer.PartnerDateOfBirth = null;
        fosterCarer.PartnerNationalInsuranceNumber = null;

        fosterCarer.Updated = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<FosterFamiliesSearchResponse> SearchFosterFamilies(
    int localAuthorityId,
    FosterFamiliesSearchRequest request)
    {
        const int defaultPageSize = 10;

        var pageNumber = request.PageNumber < 1
            ? 1
            : request.PageNumber;

        var pageSize = request.PageSize < 1
            ? defaultPageSize
            : request.PageSize;

        var query = _db.FosterChildren
            .Include(x => x.FosterCarer)
            .Where(x => x.FosterCarer.LocalAuthorityID == localAuthorityId)
            .AsQueryable();

        var totalRecords = await query.CountAsync();

        var maxPage = totalRecords == 0
            ? 1
            : (int)Math.Ceiling(totalRecords / (double)pageSize);

        if (pageNumber > maxPage)
        {
            pageNumber = maxPage;
        }

        var results = await _db.FosterChildren
            .Include(x => x.FosterCarer)
            .Where(x => x.FosterCarer.LocalAuthorityID == localAuthorityId)
            .OrderByDescending(x => x.SubmissionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FosterFamiliesSearchItemResponse
            {
                CarerId = x.FosterCarerId,
                ChildName = $"{x.FirstName} {x.LastName}",
                ChildDateOfBirth = x.DateOfBirth,
                EligibilityCode = x.EligibilityCode,

                CarerName =
                    $"{x.FosterCarer.FirstName} {x.FosterCarer.LastName}",

                EligibilityConfirmedOn = x.SubmissionDate,

                GracePeriodEnds = _db.WorkingFamiliesEvents
                    .Where(w => w.EligibilityCode == x.EligibilityCode)
                    .Select(w => w.GracePeriodEndDate)
                    .SingleOrDefault(),

                ValidityEndDate = x.ValidityEndDate

            })
            .AsNoTracking()
            .ToListAsync();

        foreach (var item in results)
        {
            var reconfirmation =
                WorkingFamiliesCheckHelper.SetReconfirmationProperties(
                    item.ValidityEndDate.ToString(),
                    item.GracePeriodEnds.ToString(),
                    item.EligibilityConfirmedOn,
                    EligibilityCodeType.Foster,
                    item.ChildDateOfBirth.ToString());

            item.ReconfirmationStatus = reconfirmation.Status.ToString();
            item.ReconfirmBetween =
                $"{reconfirmation.StartDate:dd MMMM yyyy} and {reconfirmation.EndDate:dd MMMM yyyy}";
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

        if (includeFosterCarer)
        {
            result = await _db.FosterChildren
                .Where(x =>
                    x.FosterChildId == fosterChildId &&
                    x.FosterCarer.LocalAuthorityID == localAuthorityId)
                .Select(x => new FosterChildResponse
                {
                    FosterChildId = x.FosterChildId,

                    EligibilityCode = x.EligibilityCode,

                    EligibilityConfirmedOn = x.SubmissionDate,

                    // Needed for reconfirmation logic
                    ValidityStartDate = x.ValidityStartDate,
                    ValidityEndDate = x.ValidityEndDate,

                    GracePeriodEnds = _db.WorkingFamiliesEvents
                        .Where(w => w.EligibilityCode == x.EligibilityCode)
                        .Select(w => w.GracePeriodEndDate)
                        .SingleOrDefault(),

                    ChildFullName = $"{x.FirstName} {x.LastName}",
                    ChildDateOfBirth = x.DateOfBirth,
                    PostCode = x.PostCode,

                    FosterCarerId = x.FosterCarerId,

                    CarerName =
                        $"{x.FosterCarer.FirstName} {x.FosterCarer.LastName}",

                    PartnerName = x.FosterCarer.HasPartner
                        ? $"{x.FosterCarer.PartnerFirstName} {x.FosterCarer.PartnerLastName}"
                        : null
                })
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }
        else
        {
            result = await _db.FosterChildren
                .Where(x =>
                    x.FosterChildId == fosterChildId &&
                    x.FosterCarer.LocalAuthorityID == localAuthorityId)
                .Select(x => new FosterChildResponse
                {
                    FosterChildId = x.FosterChildId,

                    EligibilityCode = x.EligibilityCode,

                    EligibilityConfirmedOn = x.SubmissionDate,

                    // Needed for reconfirmation logic
                    ValidityStartDate = x.ValidityStartDate,
                    ValidityEndDate = x.ValidityEndDate,

                    GracePeriodEnds = _db.WorkingFamiliesEvents
                        .Where(w => w.EligibilityCode == x.EligibilityCode)
                        .Select(w => w.GracePeriodEndDate)
                        .SingleOrDefault(),

                    ChildFullName = $"{x.FirstName} {x.LastName}",
                    ChildDateOfBirth = x.DateOfBirth,
                    PostCode = x.PostCode,

                    FosterCarerId = x.FosterCarerId,

                    CarerName = null,
                    PartnerName = null
                })
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        if (result is null)
        {
            _logger.LogWarning(
                "Foster child with ID {FosterChildId} not found",
                fosterChildId);

            throw new NotFoundException(
                $"Foster child {fosterChildId} not found");
        }

        var reconfirmation = WorkingFamiliesCheckHelper
            .SetReconfirmationProperties(
                result.ValidityEndDate.ToString(),
                result.GracePeriodEnds.ToString(),
                result.EligibilityConfirmedOn,
                EligibilityCodeType.Foster,
                result.ChildDateOfBirth.ToString());

        result.ReconfirmationStatus = reconfirmation.Status.ToString();

        result.ReconfirmBetween =
            $"{reconfirmation.StartDate:dd MMMM yyyy} and {reconfirmation.EndDate:dd MMMM yyyy}";

        return result;
    }

    public async Task<FosterChildCreatedResponse> CreateFosterChild(
    FosterChildRequest request, int localAuthorityId, Guid fosterCarerId, DateTime submissionDate)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get existing carer
        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId && x.LocalAuthorityID == localAuthorityId);

        if (fosterCarer is null)
        {
            throw new NotFoundException(
                $"Foster carer {fosterCarerId} not found");
        }

        string eligibilityCode = await GetEligibilityCodeForFosterChild();

        // build domain model from request
        var fosterChild = BuildFosterChild(request, DateTime.UtcNow, fosterCarerId);

        // link child to current foster carer
        fosterChild.FosterCarerId = fosterCarer.FosterCarerId;
        fosterChild.EligibilityCode = eligibilityCode;

        // Create new wf event
        var workingEvent =
              WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(new FosterFamilyRequest()
              {
                  FosterCarer = new FosterCarerRequest
                  {
                      CarerFirstName = fosterCarer.FirstName,
                      CarerLastName = fosterCarer.LastName,
                      CarerDateOfBirth = fosterCarer.DateOfBirth,
                      CarerNationalInsuranceNumber = fosterCarer.NationalInsuranceNumber,
                  },
                  FosterChild = request,
                  SubmissionDate = submissionDate
              }, eligibilityCode);

        fosterChild.ValidityStartDate = workingEvent.ValidityStartDate;
        fosterChild.ValidityEndDate = workingEvent.ValidityEndDate;

        await _db.WorkingFamiliesEvents.AddAsync(workingEvent);

        fosterChild.EligibilityCode = workingEvent.EligibilityCode;

        await _db.FosterChildren.AddAsync(fosterChild);
        await _db.SaveChangesAsync();

        ReconfirmationProperties reconfirmation = WorkingFamiliesCheckHelper
            .SetReconfirmationProperties(
                workingEvent.ValidityEndDate.ToString(),
                workingEvent.GracePeriodEndDate.ToString(),
                submissionDate,
                EligibilityCodeType.Foster,
                fosterChild.DateOfBirth.ToString()
            );

        return new FosterChildCreatedResponse
        {
            ChildName = $"{fosterChild.FirstName} {fosterChild.LastName}",
            EligibilityCode = workingEvent.EligibilityCode,
            Status = "",
            EligibilityConfirmed = submissionDate,
            ReconfirmBetween = $"{reconfirmation.StartDate:dd MMMM yyyy} and {reconfirmation.EndDate:dd MMMM yyyy}",
            GracePeriodEndDate = workingEvent.GracePeriodEndDate
        };
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

        if (fosterChild is null)
        {
            throw new NotFoundException(
                $"Foster child {fosterChildId} not found");
        }

        // Working Family Event?? 

        fosterChild.FirstName = request.FosterChildRequest.ChildFirstName;
        fosterChild.LastName = request.FosterChildRequest.ChildLastName;
        fosterChild.DateOfBirth = request.FosterChildRequest.ChildDateOfBirth;
        fosterChild.PostCode = request.FosterChildRequest.ChildPostCode;

        fosterChild.Updated = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetFosterChild(fosterChildId, fosterChild.FosterCarer.LocalAuthorityID.Value, true);
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

    private static FosterCarer BuildFosterCarer(
    FosterCarerRequest request,
    FosterPartnerRequest? partner,
    bool hasPartner)
    {
        return new FosterCarer
        {
            FosterCarerId = Guid.NewGuid(),

            FirstName = request.CarerFirstName,
            LastName = request.CarerLastName,
            DateOfBirth = request.CarerDateOfBirth,
            NationalInsuranceNumber = request.CarerNationalInsuranceNumber,
            LocalAuthorityID = request.LocalAuthorityID,

            HasPartner = hasPartner,

            PartnerFirstName = partner?.PartnerFirstName,
            PartnerLastName = partner?.PartnerLastName,
            PartnerDateOfBirth = partner?.PartnerDateOfBirth,
            PartnerNationalInsuranceNumber = partner?.PartnerNationalInsuranceNumber,

            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

    private static FosterChild BuildFosterChild(
    FosterChildRequest request,
    DateTime submissionDate,
    Guid fosterCarerId)
    {
        return new FosterChild
        {
            FosterChildId = Guid.NewGuid(),

            FirstName = request.ChildFirstName,
            LastName = request.ChildLastName,
            DateOfBirth = request.ChildDateOfBirth,
            PostCode = request.ChildPostCode,

            FosterCarerId = fosterCarerId,

            SubmissionDate = submissionDate,

            Status = "Active",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

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