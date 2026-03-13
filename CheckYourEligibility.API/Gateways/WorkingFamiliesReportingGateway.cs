
using AutoMapper;

public class WorkingFamiliesReportingGateway : IWorkingFamiliesReporting
{

    protected readonly IMapper _mapper;
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public WorkingFamiliesReportingGateway(
        IMapper mapper,
        EligibilityCheckContext db,
        ILogger<WorkingFamiliesReportingGateway> logger
    )
    {
        _mapper = mapper;
        _db = db;
        _logger = logger;
    }

    public Task<WorkingFamilyEventByEligibilityCodeRepsonse> GetAllWorkingFamiliesEventsByEligibilityCode(string eligibilityCode)
    {
       return null;
    }

}