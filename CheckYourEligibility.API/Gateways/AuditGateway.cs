using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;

namespace CheckYourEligibility.API.Gateways;

public class AuditGateway : BaseGateway, IAudit
{
    private readonly IEligibilityCheckContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ILogger _logger;
    protected readonly IMapper _mapper;

    public AuditGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger.CreateLogger("UsersService");
        _db = dbContext;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> AuditAdd(AuditData data)
    {
        try
        {
            var item = _mapper.Map<Audit>(data);
            item.AuditID = Guid.NewGuid().ToString();
            item.TimeStamp = DateTime.UtcNow;

            await _db.Audits.AddAsync(item);
            await _db.SaveChangesAsync();

            return item.AuditID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Db Audit");
            throw;
        }
    }

    public AuditData? AuditDataGet(AuditType type, string id)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
                var host = httpContext.Request.Host;
                var path = httpContext.Request.Path;
                var method = httpContext.Request.Method;
                var auth = httpContext.User.Claims.FirstOrDefault(x =>
                    x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "";
                var scope = httpContext.User.Claims.FirstOrDefault(x => x.Type == "scope")?.Value ?? "";
                return new AuditData
                {
                    Type = type,
                    typeId = id,
                    url = $"{host}{path}",
                    method = method,
                    source = remoteIpAddress != null ? remoteIpAddress.ToString() : string.Empty,
                    authentication = auth,
                    scope = scope
                };
            }

            return new AuditData
            {
                Type = type,
                typeId = id,
                url = "Unknown",
                method = "Unknown",
                source = "Unknown",
                authentication = "Unknown",
                scope = "Unknown"
            };
        }
        catch (Exception ex)
        {
            var sanitizedId = id.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
            _logger.LogError(ex, "Failed to get audit data for type {Type} and ID {Id}", type, sanitizedId);
            return null;
        }
    }

    public async Task<string> CreateAuditEntry(AuditType type, string id)
    {
        try
        {
            // Get the audit data
            var auditData = AuditDataGet(type, id);
            if (auditData == null)
            {
                var sanitizedId = id.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                _logger.LogWarning("Failed to create audit data for type {Type} and ID {Id}", type, sanitizedId);
                return string.Empty;
            }

            // Add it to the database
            return await AuditAdd(auditData);
        }
        catch (Exception ex)
        {
            var sanitizedId = id.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
            _logger.LogError(ex, "Failed to create audit entry for type {Type} and ID {Id}", type, sanitizedId);
            return string.Empty;
        }
    }
}