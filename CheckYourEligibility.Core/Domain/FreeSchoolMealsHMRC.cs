using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.Core.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class FreeSchoolMealsHMRC
{
    /// <summary>
    ///     NINO
    /// </summary>
    [Column(TypeName = "varchar(50)")]
    public string FreeSchoolMealsHMRCID { get; set; }

    public int DataType { get; set; }

    public DateTime DateOfBirth { get; set; }

    [Column(TypeName = "varchar(100)")] public string Surname { get; set; }
}