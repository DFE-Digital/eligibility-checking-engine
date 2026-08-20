
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;

public class UserRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid UserRoleId { get; init; } = Guid.NewGuid();

    public string UserId { get; set; }

    public virtual User User { get; set; }

    [Column(TypeName = "varchar(200)")]
    public UserRoleName RoleName { get; set; }

}