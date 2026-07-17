using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests.DWP;

/// <summary>
/// Request body for the temporary /admin/dwp-diagnostics/citizen-match endpoint. Mirrors the
/// fields CheckingEngineGateway.DwpCitizenCheck sends for a real check (lastName + dateOfBirth +
/// NINO), so this exercises DWP CAPI matching exactly as the app does in production.
/// </summary>
public class DwpDiagnosticsMatchRequest
{
    public string LastName { get; set; }

    /// <summary>Format yyyy-MM-dd.</summary>
    public string DateOfBirth { get; set; }

    /// <summary>
    /// Full NINO - only the 4-digit fragment (nino.Substring(nino.Length - 5, 4)) is ever sent to
    /// DWP or persisted; the full value is not logged or stored.
    /// </summary>
    public string Nino { get; set; }

    public CheckEligibilityType Type { get; set; } = CheckEligibilityType.FreeSchoolMeals;
}
