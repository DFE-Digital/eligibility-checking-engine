namespace CheckYourEligibility.API.Boundary.Responses;

public class SupportEstablishmentSearchResponse
{
    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalNumberOfRecords { get; set; }

    public IEnumerable<Establishment> Data { get; set; } = [];
}