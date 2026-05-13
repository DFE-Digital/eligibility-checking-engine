using System.Net;
using CheckYourEligibility.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CAPIClaimResponseBase
    {
        public EligibilityTier? EligibilityTier { get; set; }
        public HttpStatusCode CAPIResponseCode { get; set; }
        public string Reason { get; set; }
        public string CAPIEndpoint { get; set; }
        public CheckEligibilityStatus CheckEligibilityStatus { get; set; }
    }
}