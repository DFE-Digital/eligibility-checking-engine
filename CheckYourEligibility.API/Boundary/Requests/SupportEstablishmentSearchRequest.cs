namespace CheckYourEligibility.API.Boundary.Requests;

public class SupportEstablishmentSearchRequest
{
    public string? Name { get; set; }

    public int? Urn { get; set; }

    public string? Type { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}