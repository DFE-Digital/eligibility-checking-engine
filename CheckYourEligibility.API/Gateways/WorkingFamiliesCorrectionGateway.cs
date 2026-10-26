using System.Data;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class WorkingFamiliesCorrectionGateway : IWorkingFamiliesCorrection
{
    private readonly IEligibilityCheckContext _db;

    public WorkingFamiliesCorrectionGateway(IEligibilityCheckContext db)
    {
        _db = db;
    }

    public async Task<WorkingFamiliesCorrectionRecord?> GetByEligibilityCode(
        string eligibilityCode,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var summaries = await _db.WorkingFamiliesEventSummaries
            .AsNoTracking()
            .Where(x => x.EligibilityCode == eligibilityCode)
            .Take(2)
            .ToListAsync(cancellationToken);

        var events = await _db.WorkingFamiliesEvents
            .AsNoTracking()
            .Where(x => x.EligibilityCode == eligibilityCode && !x.IsDeleted)
            .OrderByDescending(x => x.SubmissionDate)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0 && events.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (summaries.Count != 1 || events.Count == 0)
        {
            throw new WorkingFamiliesCorrectionDataException(
                "Correction requires exactly one summary and at least one non-deleted event.");
        }

        if (events.Count > 1 &&
            events[0].SubmissionDate == events[1].SubmissionDate)
        {
            throw new WorkingFamiliesCorrectionDataException(
                "Multiple events share the latest submission date.");
        }

        var record = MapRecord(summaries[0], events[0]);

        await transaction.CommitAsync(cancellationToken);

        return record;
    }

    private static WorkingFamiliesCorrectionRecord MapRecord(
        WorkingFamiliesEventSummary summary,
        WorkingFamiliesEvent latestEvent)
    {
        return new WorkingFamiliesCorrectionRecord
        {
            EligibilityCode = summary.EligibilityCode,
            SummaryId = summary.WorkingFamiliesEventSummaryID,
            LatestEventId = latestEvent.WorkingFamiliesEventID,

            Child = new WorkingFamiliesCorrectionChild
            {
                FirstName = latestEvent.ChildFirstName,
                LastName = latestEvent.ChildLastName,
                DateOfBirth = latestEvent.ChildDateOfBirth,
                Postcode = latestEvent.ChildPostCode
            },

            Carer = new WorkingFamiliesCorrectionAdult
            {
                FirstName = latestEvent.ParentFirstName,
                LastName = latestEvent.ParentLastName,
                DateOfBirth = NormaliseDateOfBirth(latestEvent.ParentDateOfBirth),
                NationalInsuranceNumber = latestEvent.ParentNationalInsuranceNumber
            },

            Partner = HasPartnerDetails(latestEvent)
                ? new WorkingFamiliesCorrectionAdult
                {
                    FirstName = latestEvent.PartnerFirstName,
                    LastName = latestEvent.PartnerLastName,
                    DateOfBirth = NormaliseDateOfBirth(latestEvent.PartnerDateOfBirth),
                    NationalInsuranceNumber = latestEvent.PartnerNationalInsuranceNumber
                }
                : null,

            ValidityStartDate = summary.ValidityStartDate,
            ValidityEndDate = summary.ValidityEndDate,
            DiscretionaryValidityStartDate = summary.DiscretionaryValidityStartDate,
            GracePeriodEndDate = summary.GracePeriodEndDate,
            LastUpdatedDate = summary.LastUpdatedDate
        };
    }

    private static bool HasPartnerDetails(WorkingFamiliesEvent record)
    {
        return !string.IsNullOrWhiteSpace(record.PartnerFirstName)
            || !string.IsNullOrWhiteSpace(record.PartnerLastName)
            || !string.IsNullOrWhiteSpace(record.PartnerNationalInsuranceNumber)
            || NormaliseDateOfBirth(record.PartnerDateOfBirth).HasValue;
    }

    private static DateTime? NormaliseDateOfBirth(DateTime? value)
    {
        return value == DateTime.MinValue ? null : value;
    }
}