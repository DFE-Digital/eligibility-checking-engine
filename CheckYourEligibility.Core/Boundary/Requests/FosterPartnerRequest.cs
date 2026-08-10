namespace CheckYourEligibility.Core.Boundary.Requests;

public class FosterPartnerRequest
{

    public string PartnerFirstName { get; set; }
    public string PartnerLastName { get; set; }
    public DateTime PartnerDateOfBirth { get; set; }
    private string? _partnerNationalInsuranceNumber;
    public string? PartnerNationalInsuranceNumber
    {
        get => _partnerNationalInsuranceNumber;
        set => _partnerNationalInsuranceNumber =
            value?.ToUpper().Replace(" ", string.Empty);
    }
}