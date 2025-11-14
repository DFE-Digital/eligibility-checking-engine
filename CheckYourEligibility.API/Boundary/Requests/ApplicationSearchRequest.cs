// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public class ApplicationSearchRequest
{
    public ApplicationSearchRequestData? Data { get; set; }

    // Pagination properties at the request level
    public int PageNumber { get; set; } = 1; // Default to page 1
    public int PageSize { get; set; } = 10; // Default to 10 items per page
    
    public ApplicationSearchRequestMeta? Meta { get; set; }

}

public class ApplicationSearchRequestMeta {
    public int PageNumber { get; set; } = 1; // Default to page 1
    public int PageSize { get; set; } = 10; // Default to 10 items per page
}

public class ApplicationSearchRequestData
{
    public CheckEligibilityType Type { get; set; } = CheckEligibilityType.FreeSchoolMeals;
    public int? LocalAuthority { get; set; }
    public int? MultiAcademyTrust { get; set; }
    public int? Establishment { get; set; }
    public IEnumerable<ApplicationStatus>? Statuses { get; set; }
    public string? ParentLastName { get; set; }
    public string? ParentNationalInsuranceNumber { get; set; }
    public string? ParentNationalAsylumSeekerServiceNumber { get; set; }
    public string? ParentDateOfBirth { get; set; }
    public string? ChildLastName { get; set; }
    public string? ChildDateOfBirth { get; set; }
    public string? Keyword { get; set; }
    public string? Reference { get; set; }
    public DateRange? DateRange { get; set; }
}

public class DateRange
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
}