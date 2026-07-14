using CheckYourEligibility.API.Domain.Enums;
using System.Net;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CAPIClaimResponseBase
    {
        public EligibilityTier? EligibilityTier { get; set; }
        public HttpStatusCode CAPIResponseCode { get; set; }
        public string Reason { get; set; }
        public string CAPIEndpoint { get; set; }
        public string RequestBody { get; set; }
        public string ResponseBody { get; set; }
        public CheckEligibilityStatus CheckEligibilityStatus { get; set; }
    }
}