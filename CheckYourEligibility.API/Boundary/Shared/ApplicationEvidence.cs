using System;

namespace CheckYourEligibility.API.Boundary.Shared;

public class ApplicationEvidence
{
    public string FileName { get; set; }
    public string FileType { get; set; }
    public string StorageAccountReference { get; set; }
}
