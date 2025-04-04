using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class FreeSchoolMealsHO
{
    /// <summary>
    ///     NASS
    /// </summary>
    [Column(TypeName = "varchar(100)")]
    public string FreeSchoolMealsHOID { get; set; } = string.Empty;

    [Column(TypeName = "varchar(50)")] public string NASS { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    [Column(TypeName = "varchar(100)")] public string LastName { get; set; } = string.Empty;
}