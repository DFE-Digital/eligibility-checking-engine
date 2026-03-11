using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// The eligibility event data sent by HMRC (TFC) in PUT requests.
/// </summary>
public class EligibilityEventData : IValidatableObject
{
    /// <summary>
    /// The HMRC-issued eligibility code (DERN). Must be exactly 11 digits.
    /// </summary>
    [JsonProperty(PropertyName = "dern")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "dern is a required field")]
    [StringLength(maximumLength: 11, MinimumLength = 11, ErrorMessage = "dern must be exactly 11 characters long")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "dern must contain only digits")]
    public string Dern { get; set; } = string.Empty;

    /// <summary>
    /// Date the event was submitted.
    /// </summary>
    [JsonProperty(PropertyName = "submissionDate")]
    [Required(ErrorMessage = "submissionDate is a required field")]
    public DateTime? SubmissionDate { get; set; }

    /// <summary>
    /// Start date of the eligibility validity window.
    /// </summary>
    [JsonProperty(PropertyName = "validityStartDate")]
    [Required(ErrorMessage = "validityStartDate is a required field")]
    public DateTime ValidityStartDate { get; set; }

    /// <summary>
    /// End date of the eligibility validity window.
    /// </summary>
    [JsonProperty(PropertyName = "validityEndDate")]
    [Required(ErrorMessage = "validityEndDate is a required field")]
    public DateTime ValidityEndDate { get; set; }

    /// <summary>
    /// Parent/applicant details.
    /// </summary>
    [JsonProperty(PropertyName = "parent")]
    [Required(ErrorMessage = "parent is a required field")]
    public ParentPartnerData? Parent { get; set; }

    /// <summary>
    /// Child details.
    /// </summary>
    [JsonProperty(PropertyName = "child")]
    [Required(ErrorMessage = "child is a required field")]
    public ChildData? Child { get; set; }

    /// <summary>
    /// Partner details (optional).
    /// </summary>
    [JsonProperty(PropertyName = "partner")]
    public ParentPartnerData? Partner { get; set; }

    /// <summary>
    /// Event timestamp provided by caller — accepted and stored for future use.
    /// </summary>
    [JsonProperty(PropertyName = "eventDateTime")]
    public DateTime? EventDateTime { get; set; }

    public override string ToString() => JsonConvert.SerializeObject(this);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidityStartDate >= ValidityEndDate)
            yield return new ValidationResult(
                "validityStartDate cannot be on or after validityEndDate",
                new[] { "ValidityStartDate", "ValidityEndDate" });

        if (SubmissionDate.HasValue && SubmissionDate.Value > ValidityStartDate)
            yield return new ValidationResult(
                "submissionDate cannot be after validityStartDate",
                new[] { "SubmissionDate", "ValidityStartDate" });
    }
}
