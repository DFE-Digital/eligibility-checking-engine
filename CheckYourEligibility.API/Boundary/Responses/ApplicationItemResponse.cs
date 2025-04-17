namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationItemResponse
{
    public ApplicationResponse Data { get; set; } = null!;
    public ApplicationResponseLinks Links { get; set; } = null!;
}