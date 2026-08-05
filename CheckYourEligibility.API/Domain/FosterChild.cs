using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FosterChild
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid FosterChildId { get; set; }
    [Column(TypeName = "varchar(100)")] public string FirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string PostCode { get; set; }

    public DateTime ValidityStartDate { get; set; }
    public DateTime ValidityEndDate { get; set; }
    public DateTime SubmissionDate { get; set; }

    [Column(TypeName = "varchar(50)")] public string Status { get; set; } = "Active";
    
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }

    public Guid FosterCarerId { get; set; }
    public FosterCarer FosterCarer { get; set; } = null!;

    [Column(TypeName = "nchar(11)")] public string EligibilityCode { get; set; } = null!;

}