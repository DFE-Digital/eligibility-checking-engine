
using AutoMapper;
using CheckYourEligibility.API.Domain;
using Microsoft.EntityFrameworkCore;

public class WorkingFamiliesReportingGateway : IWorkingFamiliesReporting
{

    protected readonly IMapper _mapper;
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public WorkingFamiliesReportingGateway(
        IMapper mapper,
        IEligibilityCheckContext db,
        ILogger<WorkingFamiliesReportingGateway> logger
    )
    {
        _mapper = mapper;
        _db = db;
        _logger = logger;
    }


    public async Task<WorkingFamilyEventByEligibilityCodeRepsonse> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode)
    {
        try
        {
            // order oldest to newest 
            var records = await _db.WorkingFamiliesEvents
                .Where(x => x.EligibilityCode == eligibilityCode && !x.IsDeleted)
                .OrderBy(x => x.SubmissionDate)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>();

            if (!records.Any())
            {
                return new WorkingFamilyEventByEligibilityCodeRepsonse
                {
                    Data = result
                };
            }

            WorkingFamiliesEvent? previous = null;

            // check if each record is a reconfirm or application event
            foreach (var current in records)
            {
                bool isReconfirm =
                    previous != null &&
                    current.ValidityStartDate <= previous.GracePeriodEndDate;

                result.Add(new WorkingFamilyEventByEligibilityCodeRepsonseItem
                {
                    Event = isReconfirm
                        ? WorkingFamilyEventType.Reconfirm
                        : WorkingFamilyEventType.Application,

                    Record = current
                });

                previous = current;
            }

            return new WorkingFamilyEventByEligibilityCodeRepsonse
            {
                Data = result
                    .OrderByDescending(x => x.Record.SubmissionDate)
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            var sanitizedEligibilityCode = eligibilityCode?
                .Replace(Environment.NewLine, "")
                .Replace("\n", "")
                .Replace("\r", "");

            _logger.LogError(
                ex,
                $"Error fetching all working family events for {sanitizedEligibilityCode}"
            );

            throw;
        }
    }


}
