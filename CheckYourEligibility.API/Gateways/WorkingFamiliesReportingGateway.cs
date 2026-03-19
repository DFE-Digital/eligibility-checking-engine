
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
            var wfRecords = await _db.WorkingFamiliesEvents.Where(x => x.EligibilityCode == eligibilityCode && !x.IsDeleted).OrderBy(x => x.DiscretionaryValidityStartDate).ToListAsync();

            if (!wfRecords.Any())
                throw new Exception($"No working family events found");

            // Only one event → treat as application
            if (wfRecords.Count == 1)
            {
                return new WorkingFamilyEventByEligibilityCodeRepsonse
                {
                    Data = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
                    {
                        new WorkingFamilyEventByEligibilityCodeRepsonseItem
                        {
                            Event = WorkingFamilyEventType.Application,
                            Record = wfRecords[0]
                        }
                    }
                };
            }


            WorkingFamiliesEvent applicationEvent = wfRecords[0];
            var blockGPED = applicationEvent.GracePeriodEndDate;

            var blocks = new List<List<WorkingFamilyEventByEligibilityCodeRepsonseItem>>();
            var currentBlock = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
            {
                // Add the first event as the application for block 1
                new WorkingFamilyEventByEligibilityCodeRepsonseItem
                {
                    Event = WorkingFamilyEventType.Application,
                    Record = wfRecords[0]
                }
            };


            // Start iterating from the second event, as the first is application.
            for (int i = 1; i < wfRecords.Count; i++)
            {
                WorkingFamiliesEvent ev = wfRecords[i];

                if (ev.DiscretionaryValidityStartDate <= blockGPED)
                {
                    // This is a reconfirmation
                    currentBlock.Add(new WorkingFamilyEventByEligibilityCodeRepsonseItem
                    {
                        Event = WorkingFamilyEventType.Reconfirm,
                        Record = ev
                    });
                }
                else
                {
                    blocks.Add(currentBlock);

                    // Start a new block
                    currentBlock = new List<WorkingFamilyEventByEligibilityCodeRepsonseItem>
                    {
                        new WorkingFamilyEventByEligibilityCodeRepsonseItem
                        {
                            Event = WorkingFamilyEventType.Application,
                            Record = ev
                        }
                    };

                    blockGPED = ev.GracePeriodEndDate;
                }
            }

            blocks.Add(currentBlock);

            return new WorkingFamilyEventByEligibilityCodeRepsonse
            {
                Data = blocks
                .OrderByDescending(block => block[0].Record.SubmissionDate)
                .SelectMany(e => e)
                .ToList()
            };

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching all working family events for ${eligibilityCode}");
            throw;
        }

    }
}
