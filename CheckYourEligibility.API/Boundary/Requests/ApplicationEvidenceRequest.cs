namespace CheckYourEligibility.API.Boundary.Requests;

public class ApplicationEvidenceRequest
{
    public string FileName { get; set; }
    public string FileType { get; set; }
    public string StorageAccountReference { get; set; }
}