using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.UseCases;

public interface IUpsertWorkingFamiliesEventUseCase
{
    Task<WorkingFamiliesEvent> Execute(string hmrcId, EligibilityEventRequest request);
}

public class UpsertWorkingFamiliesEventUseCase : IUpsertWorkingFamiliesEventUseCase
{
    private readonly IWorkingFamiliesEvent _gateway;
    private readonly IAudit _auditGateway;
    private readonly ILogger<UpsertWorkingFamiliesEventUseCase> _logger;

    public UpsertWorkingFamiliesEventUseCase(
        IWorkingFamiliesEvent gateway,
        IAudit auditGateway,
        ILogger<UpsertWorkingFamiliesEventUseCase> logger)
    {
        _gateway = gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<WorkingFamiliesEvent> Execute(string hmrcId, EligibilityEventRequest request)
    {
        var eventData = request.EligibilityEvent!;

        // Conflict check: same HMRC id but different DERN → 409
        var existing = await _gateway.GetByHMRCId(hmrcId);
        if (existing != null && existing.EligibilityCode.Trim() != eventData.Dern.Trim())
            throw new InvalidOperationException("CONFLICT");

        // Overlap check: different HMRC id, same DERN, overlapping validity dates → 400
        var overlapping = await _gateway.GetOverlappingEventsByDern(
            eventData.Dern, hmrcId, eventData.ValidityStartDate, eventData.ValidityEndDate);
        if (overlapping.Count > 0)
        {
            var overlaps = overlapping.Select(o => new OverlapDetail
            {
                EligibilityEventId = o.HMRCEligibilityEventId!,
                Dern = o.EligibilityCode.Trim(),
                ValidityStartDate = o.ValidityStartDate,
                ValidityEndDate = o.ValidityEndDate
            }).ToList();

            throw new DernOverlapException(
                hmrcId, eventData.Dern,
                eventData.ValidityStartDate, eventData.ValidityEndDate,
                overlaps);
        }

        var domain = new WorkingFamiliesEvent
        {
            WorkingFamiliesEventID = existing?.WorkingFamiliesEventID ?? Guid.NewGuid().ToString(),
            HMRCEligibilityEventId = hmrcId,
            EligibilityCode = eventData.Dern,
            ChildFirstName = eventData.Child!.Forename,
            ChildLastName = eventData.Child.Surname,
            ChildDateOfBirth = eventData.Child.Dob,
            ChildPostCode = eventData.Child.PostCode,
            ParentFirstName = eventData.Parent!.Forename,
            ParentLastName = eventData.Parent.Surname,
            ParentNationalInsuranceNumber = eventData.Parent.Nino,
            PartnerFirstName = eventData.Partner?.Forename ?? string.Empty,
            PartnerLastName = eventData.Partner?.Surname ?? string.Empty,
            PartnerNationalInsuranceNumber = eventData.Partner?.Nino,
            SubmissionDate = eventData.SubmissionDate!.Value,
            ValidityStartDate = eventData.ValidityStartDate,
            ValidityEndDate = eventData.ValidityEndDate,
            DiscretionaryValidityStartDate = WorkingFamiliesEventHelper.GetDiscretionaryStartDate(
                eventData.ValidityStartDate, eventData.SubmissionDate!.Value),
            GracePeriodEndDate = WorkingFamiliesEventHelper.GetGracePeriodEndDate(eventData.ValidityEndDate),
            IsDeleted = false,
            DeletedDateTime = null,
            // Set CreatedDateTime only for new records; gateway preserves it on updates
            CreatedDateTime = existing == null ? DateTime.UtcNow : existing.CreatedDateTime,
            EventDateTime = eventData.EventDateTime
        };

        var result = await _gateway.UpsertWorkingFamiliesEvent(domain);

        await _auditGateway.CreateAuditEntry(AuditType.WorkingFamilies, hmrcId);

        var safeId = hmrcId?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation("Working families event upserted for HMRC id {HMRCId}", safeId);

        return result;
    }
}
