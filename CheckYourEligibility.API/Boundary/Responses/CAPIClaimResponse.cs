using CheckYourEligibility.API.Domain.Enums;
using System.Net;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CAPIClaimResponse
    {
        public CheckEligibilityStatus? checkEligibilityStatus {  get; set; }  
        public HttpStatusCode CAPIResponseCode { get; set; }
        public string CAPIEndpoint { get; set; }
        public string Reason { get; set; }
    }
}
