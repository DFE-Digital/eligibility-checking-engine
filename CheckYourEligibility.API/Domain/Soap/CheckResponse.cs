public class SoapCheckResponse
{
    public string Status { get; set; }
    public string ErrorCode { get; set; }
    public string Qualifier { get; set; }
    public string? ValidityStartDate { get; set; }
    public string? ValidityEndDate { get; set; }
    public string? GracePeriodEndDate { get; set; }
    public string? ParentSurname { get; set; }
}