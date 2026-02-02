using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CheckYourEligibility.API.Domain;

public class FosterCarer : IAuditable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid FosterCarerId { get; set; }
    [Column(TypeName = "varchar(100)")] public string FirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    [Column(TypeName = "varchar(50)")] public string NationalInsuranceNumber { get; set; }
    public bool HasPartner { get; set; } = false;

    [Column(TypeName = "varchar(100)")] public string? PartnerFirstName { get; set; }
    [Column(TypeName = "varchar(100)")] public string? PartnerLastName { get; set; }
    public DateTime? PartnerDateOfBirth { get; set; }
    [Column(TypeName = "varchar(50)")] public string? PartnerNationalInsuranceNumber { get; set; }


    /// <summary>
    /// The Local Authority ID that this foster carer is registered with
    /// </summary>
    public int? LocalAuthorityID { get; set; }

    /// <summary>
    /// Navigation property to the Local Authority
    /// </summary>
    public virtual LocalAuthority? LocalAuthority { get; set; }

    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }

    public FosterChild? FosterChild { get; set; }

}