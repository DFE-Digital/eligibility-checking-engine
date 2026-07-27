using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;
using System.Net;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class CAPIClaimResponseBase
    {
        public EligibilityTier? EligibilityTier { get; set; }
        public HttpStatusCode ResponseCode { get; set; }
        public long CAPIResponseCode { get; set; }
        public string? ErrorCode { get; set; }
        public string Reason { get; set; }
        public string CAPIEndpoint { get; set; }
        public string RequestBody { get; set; }
        public string ResponseBody { get; set; }
        public CheckEligibilityStatus CheckEligibilityStatus { get; set; }

        public static long ProcessCapiResponseCode(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return 0;
            }

            try
            {
                var errorResponse = JsonConvert.DeserializeObject<DwpErrorResponse>(responseBody);

                var code = errorResponse?.Errors?
                    .Select(e => e?.Code)
                    .FirstOrDefault(c => long.TryParse(c, out _));

                return long.TryParse(code, out var capiResponseCode)
                    ? capiResponseCode
                    : 0;
            }
            catch (JsonException)
            {
                return 0;
            }
        }
    }
}