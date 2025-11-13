using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Domain
{
    [ExcludeFromCodeCoverage(Justification = "Data Model.")]
    public class ECSConflict
    {
        [Key] public int ID { get; set; }
        public string CorrelationId { get; set; }
        [Column(TypeName = "varchar(50)")] public CheckEligibilityStatus ECE_Status { get; set; }
        [Column(TypeName = "varchar(50)")] public CheckEligibilityStatus ECS_Status { get; set; }
        [Column(TypeName = "varchar(50)")] public string LastName { get; set; }
        [Column(TypeName = "varchar(50)")] public string DateOfBirth { get; set; }
        [Column(TypeName = "varchar(50)")] public string Nino { get; set; }
        [Column(TypeName = "varchar(50)")] public CheckEligibilityType Type { get; set; }
        public string Organisation { get; set; }
        public DateTime TimeStamp { get; set; }
        public string EligibilityCheckHashID { get; set; }
        public virtual EligibilityCheckHash EligibilityCheckHash { get; set; }
        public HttpStatusCode CAPIResponseCode { get; set; }
        public string CAPIEndpoint { get; set; }
        public string Reason { get; set; }


    }
}