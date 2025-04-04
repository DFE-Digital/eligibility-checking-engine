// Ignore Spelling: Fsm

using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class ApplicationRequest
{
    public ApplicationRequestData? Data { get; set; }
}

public class ApplicationRequestData
{
    public CheckEligibilityType Type { get; set; } = CheckEligibilityType.None;
    public int Establishment { get; set; }
    public string ParentFirstName { get; set; } = string.Empty;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string? ParentNationalInsuranceNumber { get; set; }
    public string? ParentNationalAsylumSeekerServiceNumber { get; set; }
    public string ParentDateOfBirth { get; set; } = string.Empty;
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public string ChildDateOfBirth { get; set; } = string.Empty;
    public string? UserId { get; set; }
}