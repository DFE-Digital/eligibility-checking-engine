using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class FreeSchoolMealsHMRC
{
    /// <summary>
    ///     NINO
    /// </summary>
    [Column(TypeName = "varchar(50)")]
    public string FreeSchoolMealsHMRCID { get; set; } = string.Empty;

    public int DataType { get; set; }

    public DateTime DateOfBirth { get; set; }

    [Column(TypeName = "varchar(100)")] public string Surname { get; set; } = string.Empty;
}