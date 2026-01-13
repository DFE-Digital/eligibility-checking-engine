using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FosterCarer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int FosterCarerId { get; set; }
    [Column(TypeName = "varchar(100)")] public string FirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string LastName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    [Column(TypeName = "varchar(50)")] public string NationalInsuranceNumber { get; set; }
    public bool HasPartner { get; set; } = false;

    [Column(TypeName = "varchar(100)")] public string? PartnerFirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string? PartnerLastName { get; set; }
    public DateOnly? PartnerDateOfBirth { get; set; }
    [Column(TypeName = "varchar(50)")] public string? PartnerNationalInsuranceNumber { get; set; }

    public int LocalAuthorityId { get; set; }

    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }

    public FosterChild? FosterChild { get; set; }

}