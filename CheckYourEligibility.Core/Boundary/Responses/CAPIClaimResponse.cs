using System.Net;
using CheckYourEligibility.Core.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.Core.Boundary.Responses
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