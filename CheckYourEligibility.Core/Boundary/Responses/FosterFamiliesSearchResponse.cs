public class FosterFamiliesSearchResponse
{
    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalNumberOfRecords { get; set; }

    public IEnumerable<FosterFamiliesSearchItemResponse> Data { get; set; }
        = [];
}
