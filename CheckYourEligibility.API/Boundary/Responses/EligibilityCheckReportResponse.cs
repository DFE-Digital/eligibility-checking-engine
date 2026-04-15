public class EligibilityCheckReportResponse
{
  public IEnumerable<EligibilityCheckReportItem> Data { get; set; }
  public int PageNumber { get; set; }
  public int PageSize { get; set; }
  public int TotalCount { get; set; }
  public int TotalPages { get; set; }
}
