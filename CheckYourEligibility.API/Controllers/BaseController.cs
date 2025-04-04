using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers;

public class BaseController : Controller
{
    private readonly IAudit _audit;

    public BaseController(IAudit audit)
    {
        _audit = audit;
    }

    /// <summary>
    ///     get and create audit item
    /// </summary>
    /// <param name="type"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    protected async Task<string> AuditAdd(AuditType type, string id)
    {
        var data = AuditDataGet(type, id);
        if (data == null) return string.Empty;
        return await _audit.AuditAdd(data);
    }

    /// <summary>
    ///     get audit item
    /// </summary>
    /// <param name="type"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    [ExcludeFromCodeCoverage(Justification = "Context")]
    protected AuditData? AuditDataGet(AuditType type, string id)
    {
        if (HttpContext != null)
        {
            var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
            var host = HttpContext.Request.Host;
            var path = HttpContext.Request.Path;
            var method = HttpContext.Request.Method;
            var auth = HttpContext.User.Claims.FirstOrDefault(x =>
                x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            return new AuditData
            {
                Type = type, 
                typeId = id, 
                url = $"{host}{path}", 
                method = method, 
                source = remoteIpAddress != null ? remoteIpAddress.ToString() : string.Empty,
                authentication = auth ?? string.Empty
            };
        }

        return new AuditData
        {
            Type = type, typeId = id, url = "Unknown", method = "Unknown", source = "Unknown",
            authentication = "Unknown"
        };
    }
}