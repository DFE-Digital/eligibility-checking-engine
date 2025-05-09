namespace CheckYourEligibility.API.Boundary.Responses;

public class Establishment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string Town { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string La { get; set; } = string.Empty;
    public double? Distance { get; set; }
    public string Type { get; set; } = string.Empty;
}