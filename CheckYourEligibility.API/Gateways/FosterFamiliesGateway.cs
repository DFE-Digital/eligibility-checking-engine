using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

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
    bool includeChildren = false)
    {
        FosterFamilyResponse? result;

        if (includeChildren)
        {
            result = await _db.FosterCarers
                .Where(x => x.FosterCarerId == fosterCarerId)
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
                .Where(x => x.FosterCarerId == fosterCarerId)
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

        var fosterCarer = BuildFosterCarer(request);
        var fosterChild = BuildFosterChild(request, fosterCarer.FosterCarerId);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var workingEvent =
               WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request);

            fosterChild.ValidityStartDate = workingEvent.ValidityStartDate;
            fosterChild.ValidityEndDate = workingEvent.ValidityEndDate;

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent);
            await _db.SaveChangesAsync();

            fosterChild.EligibilityCode = workingEvent.EligibilityCode;

            await _db.FosterCarers.AddAsync(fosterCarer);
            await _db.FosterChildren.AddAsync(fosterChild);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return new FosterFamilyCreatedResponse()
            {
                ChildName = $"{fosterChild.FirstName} {fosterChild.LastName}",
                EligiblityCode = workingEvent.EligibilityCode,
                Status = fosterChild.Status,
                EligibilityConfirmed = request.SubmissionDate.ToString(),
                ReconfirmBetween = "This still need doing",
                GracePeriodEndDate = workingEvent.GracePeriodEndDate.ToString()
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
    UpdateFosterCarerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId);

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

    public async Task DeleteFosterCarer(Guid fosterCarerId)
    {
        var fosterCarer = await _db.FosterCarers
            .Include(x => x.FosterChildren)
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId);

        if (fosterCarer is null)
        {
            throw new NotFoundException(
                $"Foster carer {fosterCarerId} not found");
        }

        _db.FosterChildren.RemoveRange(fosterCarer.FosterChildren);
        _db.FosterCarers.Remove(fosterCarer);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteFosterPartner(Guid fosterCarerId)
    {
        var fosterCarer = await _db.FosterCarers
            .SingleOrDefaultAsync(x => x.FosterCarerId == fosterCarerId);

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
            .OrderByDescending(x => x.SubmissionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FosterFamiliesSearchItemResponse
            {
                ChildName = $"{x.FirstName} {x.LastName}",
                ChildDateOfBirth = x.DateOfBirth,
                EligibilityCode = x.EligibilityCode,

                CarerName =
                    $"{x.FosterCarer.FirstName} {x.FosterCarer.LastName}",

                EligibilityConfirmedOn = x.SubmissionDate,

                ReconfirmBetween = "this still needs sorting",

                GracePeriodEnds = _db.WorkingFamiliesEvents
                    .Where(w => w.EligibilityCode == x.EligibilityCode)
                    .Select(w => w.GracePeriodEndDate)
                    .SingleOrDefault(),

                ReconfirmationStatus = "this still needs sorting"
            })
            .AsNoTracking()
            .ToListAsync();


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
    bool includeFosterCarer = false)
    {
        var response = await _db.FosterChildren
            .Include(x => x.FosterCarer)
            .Where(x => x.FosterChildId == fosterChildId)
            .Select(x => new FosterChildResponse
            {
                FosterChildId = x.FosterChildId,

                EligibilityCode = x.EligibilityCode,

                ReconfirmationStatus = "work in progress",
                CodeStatus = "work in progress",

                EligibilityConfirmedOn = x.SubmissionDate,

                ReconfirmFrom = x.ValidityStartDate,
                ReconfirmTo = x.ValidityEndDate,

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

        if (response is null)
        {
            throw new NotFoundException(
                $"Foster child {fosterChildId} not found");
        }

        return response;
    }



    #region helpers

    private static FosterCarer BuildFosterCarer(FosterFamilyRequest request)
    {
        return new FosterCarer
        {
            FosterCarerId = Guid.NewGuid(),

            FirstName = request.FosterCarer.CarerFirstName,
            LastName = request.FosterCarer.CarerLastName,
            DateOfBirth = request.FosterCarer.CarerDateOfBirth,
            NationalInsuranceNumber = request.FosterCarer.CarerNationalInsuranceNumber,

            HasPartner = request.HasPartner,

            PartnerFirstName = request.Partner?.PartnerFirstName,
            PartnerLastName = request.Partner?.PartnerLastName,
            PartnerDateOfBirth = request.Partner?.PartnerDateOfBirth,
            PartnerNationalInsuranceNumber = request.Partner?.PartnerNationalInsuranceNumber,

            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

    private static FosterChild BuildFosterChild(
    FosterFamilyRequest request,
    Guid fosterCarerId)
    {
        return new FosterChild
        {
            FosterChildId = Guid.NewGuid(),

            FirstName = request.FosterChild.ChildFirstName,
            LastName = request.FosterChild.ChildLastName,
            DateOfBirth = request.FosterChild.ChildDateOfBirth,
            PostCode = request.FosterChild.ChildPostCode,

            FosterCarerId = fosterCarerId,

            SubmissionDate = request.SubmissionDate,

            Status = "Active",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }

    #endregion
}