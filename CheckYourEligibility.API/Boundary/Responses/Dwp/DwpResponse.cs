namespace CheckYourEligibility.API.Boundary.Responses.DWP;

public class DwpMatchResponse
{
    public DwpResponse_Jsonapi Jsonapi { get; set; }
    public DwpResponse_Data Data { get; set; }

    public class DwpResponse_Data
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public DwpResponse_Attributes Attributes { get; set; }
    }

    public class DwpResponse_Attributes
    {
        public string MatchingScenario { get; set; }
    }

    public class DwpResponse_Jsonapi
    {
        public string Version { get; set; }
    }
}