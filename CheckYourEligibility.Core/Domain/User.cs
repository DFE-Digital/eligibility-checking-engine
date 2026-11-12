using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CheckYourEligibility.Core.Domain.Enums;

namespace CheckYourEligibility.Core.Domain;

public class User
{
    public string UserID { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? Created { get; init; } = DateTime.UtcNow;

    [Column(TypeName = "bit")]
    public bool? IsEnabled { get; set; } = true;

    [Column(TypeName = "varchar(200)")]
    public UserType? UserType { get; set; }


    [Column(TypeName = "datetime2")]
    public DateTime? LastLogin { get; set; }


    [Column(TypeName = "varchar(200)")] 
    public string Email { get; set; }


    [Column(TypeName = "varchar(1000)")] 
    public string Reference { get; set; }


    [Column(TypeName = "nvarchar(100)")]
    [MaxLength(100)]
    public string? UserName { get; set; }


    [Column(TypeName = "nvarchar(200)")]
    [MaxLength(200)]
    public string? DisplayName { get; set; }


    [Column(TypeName = "varchar(200)")]
    public OrganisationType? OrganisationType { get; set; }


    [Column(TypeName = "int")]
    public int? OrganisationId { get; set; }


    [Column(TypeName = "bit")]
    public bool? IsDeleted { get; set; }

    // optional navigation property for the relationship with reports
    public ICollection<EligibilityCheckReport> EligibilityCheckReports { get; }
        = new List<EligibilityCheckReport>();
}