namespace CheckYourEligibility.API.Boundary.Responses;

public class ErrorResponse
{
    public List<Error> Errors { get; set; } = [];
}

public class Error
{
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
}