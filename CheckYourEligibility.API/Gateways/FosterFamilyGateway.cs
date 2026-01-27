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

        if (fosterCarer == null)
            throw new InvalidOperationException("Mapping to FosterCarer returned null.");

        if (fosterCarer.FosterChild == null)
            throw new InvalidOperationException("FosterChild cannot be null.");

        fosterCarer.FosterCarerId = Guid.NewGuid();
        fosterCarer.FosterChild.FosterChildId = Guid.NewGuid();

        // Single transaction
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _db.FosterCarers.AddAsync(fosterCarer, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var workingEvent =
                WorkingFamiliesEventHelper.ParseWorkingFamilyFromFosterFamily(fosterCarer);

            await _db.WorkingFamiliesEvents.AddAsync(workingEvent, cancellationToken);
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

        try
        {
            var query = await _db.FosterCarers
           .AsNoTracking()
           .Where(fc => fc.FosterCarerId.ToString() == guid)
           .Include(fc => fc.FosterChild)
           .FirstOrDefaultAsync();

            if (query != null)
            {
                FosterFamilyResponse response = _mapper.Map<FosterFamilyResponse>(query);
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving foster family with GUID: -", guid);
            throw new NotFoundException($"Unable to find foster family: - {guid}, {ex.Message}");
        }

        return null;
    }
}