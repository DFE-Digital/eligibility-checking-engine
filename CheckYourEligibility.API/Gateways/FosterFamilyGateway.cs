using AutoMapper;

using CheckYourEligibility.API.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

public class FosterFamilyGateway : IFosterFamily
{
    protected readonly IMapper _mapper;
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public FosterFamilyGateway(
        IMapper mapper,
        IEligibilityCheckContext db,
        ILogger<FosterFamilyGateway> logger
    )
    {
        _mapper = mapper;
        _db = db;
        _logger = logger;
    }

    public async Task<FosterFamilyResponse> PostFosterFamily(FosterFamilyRequestData data, CancellationToken cancellationToken = default)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        var fosterCarer = _mapper.Map<FosterCarer>(data);
        var fosterChild = _mapper.Map<FosterChild>(data);

        if (fosterCarer == null)
            throw new InvalidOperationException("Mapping to FosterCarer returned null.");

        if(fosterChild == null)
            throw new InvalidOperationException("Mapping to FosterChild returned null.");

        fosterCarer.FosterCarerId = Guid.NewGuid();
        fosterChild.FosterChildId = Guid.NewGuid();

        // Single transaction
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var workingEvent =
               WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(fosterCarer);

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            await _db.FosterCarers.AddAsync(fosterCarer, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            fosterChild.EligibilityCode = workingEvent.EligibilityCode;
            await _db.FosterChildren.AddAsync(fosterChild, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        
            await tx.CommitAsync(cancellationToken);

            // Return response
            return _mapper.Map<FosterFamilyResponse>(fosterCarer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating foster family and working families event");
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<FosterFamilyResponse?> GetFosterFamily(string guid)
    {

        var safeGuid = guid?
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        try
        {
            var query = await _db.FosterCarers
           .AsNoTracking()
           .Where(fc => fc.FosterCarerId.ToString() == guid)
           .Include(fc => fc.FosterChildren)
           .FirstOrDefaultAsync();

            if (query != null)
            {
                FosterFamilyResponse response = _mapper.Map<FosterFamilyResponse>(query);
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving foster family with GUID: -", safeGuid);
            throw new NotFoundException($"Unable to find foster family: - {guid}, {ex.Message}");
        }

        return null;
    }

    public async Task<FosterFamilyResponse> UpdateFosterFamily(string guid, FosterFamilyUpdateRequest data, CancellationToken cancellationToken = default)
    {

        var safeGuid = guid?
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // find and map foster carer
            var fosterCarer = await _db.FosterCarers.FirstOrDefaultAsync(fc => fc.FosterCarerId.ToString() == guid, cancellationToken);
            if (fosterCarer is null) throw new NotFoundException("Unable to find the foster carer");
            _mapper.Map(data, fosterCarer);

            // find and map foster child
            var fosterChild = await _db.FosterChildren.FirstOrDefaultAsync(fc => fc.FosterCarerId.ToString() == guid, cancellationToken);
            if (fosterChild is null) throw new NotFoundException("Unable to find the foster child");
            _mapper.Map(data, fosterChild);

            // Ensure the foster child is associated with the foster carer
            fosterCarer.FosterChildren.Add(fosterChild);

            // find and map working family event
            var workingFamiliesEvent = await _db.WorkingFamiliesEvents
                .FirstOrDefaultAsync(
                    wf => wf.EligibilityCode == fosterCarer.FosterChildren.First().EligibilityCode,
                    cancellationToken);

            if (workingFamiliesEvent is null)
                throw new NotFoundException("Unable to find the working family event");

            _mapper.Map(data, workingFamiliesEvent);

            // Save all changes
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return _mapper.Map<FosterFamilyResponse>(fosterCarer);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error updating foster family with GUID: {Guid}", safeGuid);
            throw new NotFoundException($"Unable to update foster family: {guid}, {ex.Message}");
        }
    }



}