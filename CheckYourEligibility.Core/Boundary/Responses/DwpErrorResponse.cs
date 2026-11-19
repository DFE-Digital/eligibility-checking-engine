namespace CheckYourEligibility.Core.Boundary.Responses
{
    using Newtonsoft.Json;

    public  class DwpErrorResponse
    {
        [JsonProperty("errors")]
        public DwpError[] Errors { get; set; }
    }

    public class DwpError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("source")]
        public Source Source { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    public  class Source
    {
        [JsonProperty("pointer")]
        public string Pointer { get; set; }
    }
}
