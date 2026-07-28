using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Net;


namespace CheckYourEligibility.API.Domain
{
    [ExcludeFromCodeCoverage(Justification = "Data Model.")]
    public class CAPIAudit
    {
        public CAPIAudit()
        {
        }

        [Key]
        public int AuditId { get; set; }
        public Guid DWPCorrelationId { get; set; }
        public Guid EligibilityCheckId { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public string ResponseBody { get; set; } = string.Empty;
        public HttpStatusCode ResponseCode {get;set;}
        public long CAPIResponseCode { get; set; }

        public CAPIAudit(Guid eligibilityCheckId, Guid dwpCorrelationId,
            string endpoint,
            string requestBody,
            string responseBody,
            HttpStatusCode responseCode,
            long capiResponseCode)
        {
            EligibilityCheckId = eligibilityCheckId;
            DWPCorrelationId = dwpCorrelationId;
            Endpoint = endpoint ?? string.Empty;
            RequestBody = requestBody ?? string.Empty;
            ResponseBody = responseBody ?? string.Empty;
            ResponseCode = responseCode;
            CAPIResponseCode = capiResponseCode;
            TimeStamp = DateTime.UtcNow;
        }

    }
}
