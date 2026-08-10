using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Database;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Exceptions;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.Core.Gateways;

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

    public async Task<FosterFamilyCreatedResponse> CreateFosterFamily(FosterFamilyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fosterCarer = BuildFosterCarer(request.FosterCarer, request.Partner, request.HasPartner);
        var fosterChild = BuildFosterChild(request.FosterChild, request.SubmissionDate, fosterCarer.FosterCarerId);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var workingEvent = WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(request);

            fosterChild.ValidityStartDate = workingEvent.ValidityStartDate;
            fosterChild.ValidityEndDate = workingEvent.ValidityEndDate;

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent);

            fosterChild.EligibilityCode = workingEvent.EligibilityCode;

            await _db.FosterCarers.AddAsync(fosterCarer);
            await _db.FosterChildren.AddAsync(fosterChild);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return new FosterFamilyCreatedResponse()
            {
                FosterCarerId = fosterCarer.FosterCarerId,
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
    int localAuthorityId,
    bool includeFosterCarer = false)
    {
        FosterChildResponse? result;

        if (includeFosterCarer)
        {
            result = await _db.FosterChildren
                .Where(x => x.FosterChildId == fosterChildId && x.FosterCarer.LocalAuthorityID == localAuthorityId)
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
        }
        else
        {
            result = await _db.FosterChildren
                .Where(x => x.FosterChildId == fosterChildId && x.FosterCarer.LocalAuthorityID == localAuthorityId)
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

        // build domain model from request
        var fosterChild = BuildFosterChild(request, DateTime.UtcNow, fosterCarerId);

        // link child to current foster carer
        fosterChild.FosterCarerId = fosterCarer.FosterCarerId;

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
              });

        fosterChild.ValidityStartDate = workingEvent.ValidityStartDate;
        fosterChild.ValidityEndDate = workingEvent.ValidityEndDate;

        await _db.WorkingFamiliesEvents.AddAsync(workingEvent);

        fosterChild.EligibilityCode = workingEvent.EligibilityCode;

        await _db.FosterChildren.AddAsync(fosterChild);
        await _db.SaveChangesAsync();

        return new FosterChildCreatedResponse
        {
            ChildName = $"{fosterChild.FirstName} {fosterChild.LastName}",
            EligiblityCode = workingEvent.EligibilityCode,
            Status = fosterChild.Status,
            EligibilityConfirmed = submissionDate.ToString(),
            ReconfirmBetween = "This still need doing",
            GracePeriodEndDate = workingEvent.GracePeriodEndDate.ToString()
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
            .SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId);

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

    public async Task DeleteFosterChild(Guid fosterChildId)
    {
        var fosterChild = await _db.FosterChildren
            .SingleOrDefaultAsync(x => x.FosterChildId == fosterChildId);

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


    #endregion
}