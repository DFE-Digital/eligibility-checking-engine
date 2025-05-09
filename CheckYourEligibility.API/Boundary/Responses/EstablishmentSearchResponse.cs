namespace CheckYourEligibility.API.Boundary.Responses;

public class EstablishmentSearchResponse
{
    public IEnumerable<Establishment> Data { get; set; } = new List<Establishment>();
}