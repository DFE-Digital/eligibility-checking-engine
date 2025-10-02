using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain
{
    [ExcludeFromCodeCoverage(Justification = "Data Model.")]
    public class ECSConflict
    {
       public int ID { get; set; }
       public string CorrelationId { get; set; }
       [Column(TypeName = "varchar(50)")] public CheckEligibilityStatus ECE_Status { get; set; }
       [Column(TypeName = "varchar(50)")] public CheckEligibilityStatus ECS_Status { get; set; }
       [Column(TypeName = "varchar(50)")] public string LastName { get; set; }
       [Column(TypeName = "varchar(50)")] public string DateOfBirth { get; set; }
       [Column(TypeName = "varchar(50)")] public string Nino { get; set; }
       [Column(TypeName = "varchar(50)")] public CheckEligibilityType Type { get; set; }
       public string Organisation { get; set; }
       public DateTime TimeStamp { get; set; }
       [Column(TypeName = "varchar(MAX)")] public string HashId { get; set; }
   
    }
}