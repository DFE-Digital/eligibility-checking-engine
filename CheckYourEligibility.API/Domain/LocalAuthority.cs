using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace CheckYourEligibility.API.Domain;

[ExcludeFromCodeCoverage(Justification = "Data Model.")]
public class LocalAuthority
{
    [Key] public int LocalAuthorityID { get; set; }

    public string LaName { get; set; }
}