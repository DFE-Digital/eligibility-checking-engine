using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.Core.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class FreeSchoolMealsHO
{
    /// <summary>
    ///     NASS
    /// </summary>
    [Column(TypeName = "varchar(100)")]
    public string FreeSchoolMealsHOID { get; set; }

    [Column(TypeName = "varchar(50)")] public string NASS { get; set; }

    public DateTime DateOfBirth { get; set; }

    [Column(TypeName = "varchar(100)")] public string LastName { get; set; }
}