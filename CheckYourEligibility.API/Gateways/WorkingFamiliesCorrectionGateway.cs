using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Validation;
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

        var selected = await SelectRecord(
            eligibilityCode,
            trackChanges: false,
            cancellationToken: cancellationToken);

        var record = selected.HasValue
            ? MapRecord(selected.Value.Summary, selected.Value.LatestEvent)
            : null;

        await transaction.CommitAsync(cancellationToken);

        return record;
    }

    public Task<WorkingFamiliesCorrectionRecord?> UpdateCarer(
        string eligibilityCode,
        WorkingFamiliesAdultCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        return UpdateRecord(
            eligibilityCode,
            request.ExpectedVersion,
            (summary, latestEvent) =>
            {
                var dateOfBirth = DateTime.ParseExact(
                    request.DateOfBirth!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

                var nationalInsuranceNumber =
                    NinoValidation.Normalize(request.NationalInsuranceNumber);

                latestEvent.ParentFirstName = request.FirstName!;
                latestEvent.ParentLastName = request.LastName!;
                latestEvent.ParentDateOfBirth = dateOfBirth;
                latestEvent.ParentNationalInsuranceNumber =
                    nationalInsuranceNumber;

                summary.ParentLastName = request.LastName!;
                summary.ParentNationalInsuranceNumber =
                    nationalInsuranceNumber!;
            },
            cancellationToken);
    }

    public Task<WorkingFamiliesCorrectionRecord?> UpdateChild(
        string eligibilityCode,
        WorkingFamiliesChildCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        return UpdateRecord(
            eligibilityCode,
            request.ExpectedVersion,
            (summary, latestEvent) =>
            {
                var dateOfBirth = DateTime.ParseExact(
                    request.DateOfBirth!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

                latestEvent.ChildFirstName = request.FirstName!;
                latestEvent.ChildLastName = request.LastName!;
                latestEvent.ChildDateOfBirth = dateOfBirth;
                latestEvent.ChildPostCode = request.Postcode!;

                summary.ChildFirstName = request.FirstName!;
                summary.ChildFirstNameTruncated = request.FirstName!
                    .Replace("-", " ")
                    .Split(" ")
                    .First()
                    .ToLower()
                    .Trim();

                summary.ChildDateOfBirth = dateOfBirth;
                summary.ChildPostCode = request.Postcode!;
            },
            cancellationToken);
    }

    private async Task<WorkingFamiliesCorrectionRecord?> UpdateRecord(
        string eligibilityCode,
        string? expectedVersion,
        Action<WorkingFamiliesEventSummary, WorkingFamiliesEvent> applyChanges,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var selected = await SelectRecord(
            eligibilityCode,
            trackChanges: true,
            cancellationToken: cancellationToken);

        if (!selected.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var summary = selected.Value.Summary;
        var latestEvent = selected.Value.LatestEvent;

        var currentVersion = CreateVersion(summary, latestEvent);

        if (!string.Equals(
            expectedVersion,
            currentVersion,
            StringComparison.Ordinal))
        {
            throw new WorkingFamiliesCorrectionConflictException();
        }

        applyChanges(summary, latestEvent);

        summary.LastUpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var persisted = await SelectRecord(
            eligibilityCode,
            trackChanges: false,
            cancellationToken: cancellationToken);

        if (!persisted.HasValue)
        {
            throw new WorkingFamiliesCorrectionDataException(
                "The correction record could not be retrieved after saving.");
        }

        var record = MapRecord(
            persisted.Value.Summary,
            persisted.Value.LatestEvent);

        await transaction.CommitAsync(cancellationToken);

        return record;
    }

    private async Task<(
        WorkingFamiliesEventSummary Summary, WorkingFamiliesEvent LatestEvent)?> SelectRecord(
        string eligibilityCode,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var summaryQuery = _db.WorkingFamiliesEventSummaries
            .Where(x => x.EligibilityCode == eligibilityCode);

        var eventQuery = _db.WorkingFamiliesEvents
            .Where(x => x.EligibilityCode == eligibilityCode && !x.IsDeleted);

        if (trackChanges)
        {
            summaryQuery = summaryQuery.AsTracking();
            eventQuery = eventQuery.AsTracking();
        }
        else
        {
            summaryQuery = summaryQuery.AsNoTracking();
            eventQuery = eventQuery.AsNoTracking();
        }

        var summaries = await summaryQuery
            .Take(2)
            .ToListAsync(cancellationToken);

        var events = await eventQuery
            .OrderByDescending(x => x.SubmissionDate)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0 && events.Count == 0)
        {
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

        return (summaries[0], events[0]);
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
            Version = CreateVersion(summary, latestEvent),

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

    private static string CreateVersion(
    WorkingFamiliesEventSummary summary,
    WorkingFamiliesEvent latestEvent)
    {
        var state = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Summary = summary,
            LatestEvent = latestEvent
        });

        var hash = SHA256.HashData(state);

        return $"v1.{Convert.ToHexString(hash)}";
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