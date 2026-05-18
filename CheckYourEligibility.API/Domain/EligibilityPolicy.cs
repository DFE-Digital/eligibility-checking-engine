using CheckYourEligibility.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain
{
    [ExcludeFromCodeCoverage(Justification = "Data Model.")]
    public class EligibilityPolicy
    {
        [Key]
        public int ID { get; set; }
        [Column(TypeName = "varchar(50)")] public string PolicyName { get; set; }

        [Column(TypeName = "varchar(100)")] public CheckEligibilityType CheckType { get; set; }

        public double UniversalCreditThreshold { get; set; }

        [Column(TypeName = "varchar(25)")] public EligibilityCriteria EligibilityCriteria { get; set; }

        public bool IsDeleted { get; set; }


    }
}
