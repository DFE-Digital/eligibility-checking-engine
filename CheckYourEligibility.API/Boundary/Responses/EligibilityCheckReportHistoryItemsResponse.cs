using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Responses
{
    public class EligibilityCheckReportItemsResponse
    {
        public List<CheckItem> Data { get; set; }
    }
    public class CheckItem { 
            
        public string ParentName { get; set; }
        public string NationalInsuranceNumber  { get; set; }
        public string DateOfBirth { get; set; }
        public string CheckSubmittedDate { get; set; }
        public string Outcome { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Tier { get; set; }
        public string CheckType { get; set; }
        public string CheckedBy { get; set; }
       
    }
}