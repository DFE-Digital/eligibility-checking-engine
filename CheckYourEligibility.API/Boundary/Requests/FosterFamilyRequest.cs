public class FosterFamilyRequest
{
    public FosterFamilyRequestData Data { get; set; }
}

public class FosterFamilyRequestData
{
    public string CarerFirstName { get; set; }
    public string CarerLastName { get; set; }
    public DateTime CarerDateOfBirth { get; set; }
    public string CarerNationalInsuranceNumber { get; set; }

    public bool HasPartner { get; set; }

    public string? PartnerFirstName { get; set; }
    public string? PartnerLastName { get; set; }
    public DateTime? PartnerDateOfBirth { get; set; }
    public string? PartnerNationalInsuranceNumber { get; set; }

    public string ChildFirstName { get; set; }
    public string ChildLastName { get; set; }
    public DateTime ChildDateOfBirth { get; set; }
    public string ChildPostCode { get; set; }

    public DateTime SubmissionDate { get; set; }
}