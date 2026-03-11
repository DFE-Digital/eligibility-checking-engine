using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Parent or partner details within an eligibility event.
/// </summary>
public class ParentPartnerData
{
    /// <summary>
    /// National Insurance number (optional, max 10 chars).
    /// </summary>
    [JsonProperty(PropertyName = "nino")]
    [MaxLength(10, ErrorMessage = "nino cannot be greater than 10 characters")]
    public string? Nino { get; set; }

    /// <summary>
    /// First name (required, max 100 chars).
    /// </summary>
    [JsonProperty(PropertyName = "forename")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "forename is a required field")]
    [MaxLength(100, ErrorMessage = "forename cannot be greater than 100 characters")]
    public string Forename { get; set; } = string.Empty;

    /// <summary>
    /// Surname (required, max 100 chars).
    /// </summary>
    [JsonProperty(PropertyName = "surname")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "surname is a required field")]
    [MaxLength(100, ErrorMessage = "surname cannot be greater than 100 characters")]
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth — accepted and stored for future use.
    /// </summary>
    [JsonProperty(PropertyName = "dob")]
    public DateTime? Dob { get; set; }

    public override string ToString() => JsonConvert.SerializeObject(this);
}
