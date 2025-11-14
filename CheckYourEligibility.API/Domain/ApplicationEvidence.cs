using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class ApplicationEvidence
{
    public int ApplicationEvidenceID { get; set; }

    [Column(TypeName = "varchar(255)")] public string FileName { get; set; }

    [Column(TypeName = "varchar(50)")] public string FileType { get; set; }

    [Column(TypeName = "varchar(500)")] public string StorageAccountReference { get; set; }

    public virtual Application Application { get; set; }

    public string ApplicationID { get; set; }
}