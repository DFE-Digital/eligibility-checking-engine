namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationSearchResponse
{
    public IEnumerable<ApplicationResponse> Data { get; set; }

    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    
    public ApplicationSearchResponseMeta Meta { get; set; }
}

public class ApplicationSearchResponseMeta
{
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
}