using Newtonsoft.Json;

namespace CheckYourEligibility.API.Boundary.Requests.DWP;

public class CitizenMatchRequest
{
    [JsonProperty("jsonapi")] public CitizenMatchRequest_Jsonapi Jsonapi { get; set; }

    [JsonProperty("data")] public CitizenMatchRequest_Data Data { get; set; }

    public class CitizenMatchRequest_Data
    {
        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("attributes")] public CitizenMatchRequest_Attributes Attributes { get; set; }
    }

    public class CitizenMatchRequest_Jsonapi
    {
        [JsonProperty("version")] public string Version { get; set; }
    }

    public class CitizenMatchRequest_Attributes
    {
        [JsonProperty("dateOfBirth")] public string DateOfBirth { get; set; }

        [JsonProperty("ninoFragment")] public string NinoFragment { get; set; }

        [JsonProperty("lastName")] public string LastName { get; set; }
    }
}