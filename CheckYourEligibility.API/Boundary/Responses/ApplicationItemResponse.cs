namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationItemResponse
{
    public ApplicationResponse Data { get; set; } = new();
    public ApplicationResponseLinks Links { get; set; } = new();
}