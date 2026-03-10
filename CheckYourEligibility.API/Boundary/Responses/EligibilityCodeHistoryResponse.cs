public class EligibilityCodeHistoryResponse
{
    public IEnumerable<EligibilityCodeHistoryResponseItem> Data { get; set; }
}

public class EligibilityCodeHistoryResponseItem
{
    public string Event { get; set; }
    public DateTime? SubmittedOn { get; set; }

    public DateTime? DiscretionaryStartDate { get; set; }
    public DateTime? ValidityStartDate { get; set; }
    public DateTime? ValidityEndDate { get; set; }
    public DateTime? GracePeriodEnds { get; set; }

    public string EventId { get; set; }

    public string ParentNationalInsuranceNumber { get; set; }
    public string ParentFirstName { get; set; }
    public string ParentLastName { get; set; }
    public DateTime? ParentDateOfBirth { get; set; }

    public string PartnerNationalInsuranceNumber { get; set; }
    public string PartnerFirstName { get; set; }
    public string PartnerLastName { get; set; }
    public DateTime? PartnerDateOfBirth { get; set; }

    public string ChildFirstName { get; set; }
    public string ChildLastName { get; set; }
    public DateTime? ChildDateOfBirth { get; set; }
    public string ChildPostcode { get; set; }

}