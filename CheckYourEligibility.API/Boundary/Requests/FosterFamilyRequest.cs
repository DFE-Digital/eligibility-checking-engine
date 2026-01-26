public class FosterFamilyRequest
{
    public FosterFamilyRequestData Data { get; set; }
}

public class FosterFamilyRequestData
{
    public string CarerFirstName { get; set; }
    public string CarerLastName { get; set; }
    public DateOnly CarerDateOfBirth { get; set; }
    public string CarerNationalInsuranceNumber { get; set; }

    public bool HasPartner { get; set; }

    public string? PartnerFirstName { get; set; }
    public string? PartnerLastName { get; set; }
    public DateOnly? PartnerDateOfBirth { get; set; }
    public string? PartnerNationalInsuranceNumber { get; set; }

    public string ChildFirstName { get; set; }
    public string ChildLastName { get; set; }
    public DateOnly ChildDateOfBirth { get; set; }
    public string ChildPostCode { get; set; }

    public DateOnly SubmissionDate { get; set; }
}