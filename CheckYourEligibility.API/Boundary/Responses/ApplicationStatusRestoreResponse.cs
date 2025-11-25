namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationStatusRestoreResponse
{
    public ApplicationStatusRestoreResponseData Data { get; set; }
}

public class ApplicationStatusRestoreResponseData
{
    public string Status { get; set; }
    public DateTime Updated { get; set; }
}