namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationSaveItemResponse
{
    public ApplicationResponse Data { get; set; } = null!;
    public ApplicationResponseLinks Links { get; set; } = null!;
}