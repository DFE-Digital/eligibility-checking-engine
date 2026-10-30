namespace CheckYourEligibility.API.Boundary.Responses;

public class WorkingFamiliesCorrectionValidationErrorResponse
{
    public List<WorkingFamiliesCorrectionValidationError> Errors { get; set; } = [];
}

public class WorkingFamiliesCorrectionValidationError
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}