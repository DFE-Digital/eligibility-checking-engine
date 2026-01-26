using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FosterChild
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid FosterChildId { get; set; }

    [Column(TypeName = "varchar(100)")] public string FirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string LastName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string PostCode { get; set; }

    public DateOnly ValidityStartDate { get; set; }
    public DateOnly ValidityEndDate { get; set; }
    public DateOnly SubmissionDate { get; set; }
    
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }

    public Guid FosterCarerId { get; set; }
    public FosterCarer FosterCarer { get; set; } = null!;
}