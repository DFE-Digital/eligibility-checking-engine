using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace CheckYourEligibility.API.Boundary.Requests;

/// <summary>
/// Wrapper for the eligibility event PUT request body.
/// </summary>
public class EligibilityEventRequest : IValidatableObject
{
    /// <summary>
    /// The eligibility event payload.
    /// </summary>
    [JsonProperty(PropertyName = "eligibilityEvent")]
    [Required(ErrorMessage = "eligibilityEvent is missing from the request body")]
    public EligibilityEventData? EligibilityEvent { get; set; }

    public override string ToString() => JsonConvert.SerializeObject(this);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EligibilityEvent == null)
            yield return new ValidationResult("Unable to deserialise eligibilityEvent from the request body", new[] { "EligibilityEvent" });
    }
}
