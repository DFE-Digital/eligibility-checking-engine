using System.Text.Json.Serialization;

namespace CheckYourEligibility.API.Boundary.Requests.DWP;

public class CitizenMatchRequest
{
    [JsonPropertyName("jsonapi")] public CitizenMatchRequest_Jsonapi Jsonapi { get; set; } = null!;

    [JsonPropertyName("data")] public CitizenMatchRequest_Data Data { get; set; } = null!;

    public class CitizenMatchRequest_Data
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;

        [JsonPropertyName("attributes")] public CitizenMatchRequest_Attributes Attributes { get; set; } = null!;
    }

    public class CitizenMatchRequest_Jsonapi
    {
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    }

    public class CitizenMatchRequest_Attributes
    {
        [JsonPropertyName("dateOfBirth")] public string DateOfBirth { get; set; } = string.Empty;

        [JsonPropertyName("ninoFragment")] public string NinoFragment { get; set; } = string.Empty;

        [JsonPropertyName("lastName")] public string LastName { get; set; } = string.Empty;
    }
}