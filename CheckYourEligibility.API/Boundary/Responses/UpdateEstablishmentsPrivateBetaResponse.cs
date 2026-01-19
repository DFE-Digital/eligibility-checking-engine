namespace CheckYourEligibility.API.Boundary.Responses;

public class UpdateEstablishmentsPrivateBetaResponse
{
    public string Message { get; set; }
    public int TotalRecords { get; set; }
    public int UpdatedCount { get; set; }
    public int NotFoundCount { get; set; }
    public List<int> NotFoundEstablishmentIds { get; set; } = new();
}
