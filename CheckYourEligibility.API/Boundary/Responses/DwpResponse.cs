namespace CheckYourEligibility.API.Boundary.Responses.DWP;

public class DwpMatchResponse
{
    public DwpResponse_Jsonapi Jsonapi { get; set; } = null!;
    public DwpResponse_Data Data { get; set; } = null!;

    public class DwpResponse_Data
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DwpResponse_Attributes Attributes { get; set; } = null!;
    }

    public class DwpResponse_Attributes
    {
        public string MatchingScenario { get; set; } = string.Empty;
    }

    public class DwpResponse_Jsonapi
    {
        public string Version { get; set; } = string.Empty;
    }
}