using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class WorkingFamiliesEventGateway : IWorkingFamiliesEvent
{
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public WorkingFamiliesEventGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext)
    {
        _logger = logger.CreateLogger("WorkingFamiliesEventGateway");
        _db = dbContext;
    }

    /// <inheritdoc />
    public async Task<WorkingFamiliesEvent?> GetByHMRCId(string hmrcId)
    {
        return await _db.WorkingFamiliesEvents
            .FirstOrDefaultAsync(x => x.HMRCEligibilityEventId == hmrcId);
    }

    /// <inheritdoc />
    public async Task<WorkingFamiliesEvent> UpsertWorkingFamiliesEvent(WorkingFamiliesEvent data)
    {
        var existing = await _db.WorkingFamiliesEvents
            .FirstOrDefaultAsync(x => x.HMRCEligibilityEventId == data.HMRCEligibilityEventId);

        if (existing != null)
        {
            // Overwrite all mapped fields, reset soft-delete state; preserve original CreatedDateTime
            existing.EligibilityCode = data.EligibilityCode;
            existing.ChildFirstName = data.ChildFirstName;
            existing.ChildLastName = data.ChildLastName;
            existing.ChildDateOfBirth = data.ChildDateOfBirth;
            existing.ChildPostCode = data.ChildPostCode;
            existing.ParentFirstName = data.ParentFirstName;
            existing.ParentLastName = data.ParentLastName;
            existing.ParentNationalInsuranceNumber = data.ParentNationalInsuranceNumber;
            existing.PartnerFirstName = data.PartnerFirstName;
            existing.PartnerLastName = data.PartnerLastName;
            existing.PartnerNationalInsuranceNumber = data.PartnerNationalInsuranceNumber;
            existing.SubmissionDate = data.SubmissionDate;
            existing.ValidityStartDate = data.ValidityStartDate;
            existing.ValidityEndDate = data.ValidityEndDate;
            existing.DiscretionaryValidityStartDate = data.DiscretionaryValidityStartDate;
            existing.GracePeriodEndDate = data.GracePeriodEndDate;
            existing.EventDateTime = data.EventDateTime;
            existing.IsDeleted = false;
            existing.DeletedDateTime = null;
            // CreatedDateTime is intentionally NOT updated — it records the original creation time

            await _db.SaveChangesAsync();
            return existing;
        }

        _db.WorkingFamiliesEvents.Add(data);
        await _db.SaveChangesAsync();
        return data;
    }

    /// <inheritdoc />
    public async Task<List<WorkingFamiliesEvent>> GetOverlappingEventsByDern(
        string dern, string excludeHmrcId, DateTime validityStart, DateTime validityEnd)
    {
        return await _db.WorkingFamiliesEvents
            .Where(e => e.EligibilityCode.Trim() == dern.Trim()
                     && e.HMRCEligibilityEventId != excludeHmrcId
                     && !e.IsDeleted
                     && e.ValidityStartDate < validityEnd
                     && e.ValidityEndDate > validityStart)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWorkingFamiliesEvent(string hmrcId)
    {
        var existing = await _db.WorkingFamiliesEvents
            .FirstOrDefaultAsync(x => x.HMRCEligibilityEventId == hmrcId && !x.IsDeleted);

        if (existing == null)
            return false;

        existing.IsDeleted = true;
        existing.DeletedDateTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
