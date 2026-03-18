using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Child details within an eligibility event.
/// </summary>
public class ChildData
{
    /// <summary>
    /// First name (required, max 26 chars).
    /// </summary>
    [JsonProperty(PropertyName = "forename")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "forename is a required field")]
    [MaxLength(26, ErrorMessage = "forename cannot be greater than 26 characters")]
    public string Forename { get; set; } = string.Empty;

    /// <summary>
    /// Surname (required, max 40 chars).
    /// </summary>
    [JsonProperty(PropertyName = "surname")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "surname is a required field")]
    [MaxLength(40, ErrorMessage = "surname cannot be greater than 40 characters")]
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth (required).
    /// </summary>
    [JsonProperty(PropertyName = "dob")]
    [Required(ErrorMessage = "dob is a required field")]
    public DateTime Dob { get; set; }

    /// <summary>
    /// Post code (optional, max 9 chars). Must match UK postcode format if provided.
    /// </summary>
    [JsonProperty(PropertyName = "postCode")]
    [MaxLength(9, ErrorMessage = "postCode cannot be greater than 9 characters")]
    [RegularExpression(@"^[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}$",
        ErrorMessage = "postCode must be a valid UK postcode")]
    public string? PostCode { get; set; }

    public override string ToString() => JsonConvert.SerializeObject(this);
}
