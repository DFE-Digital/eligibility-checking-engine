using CheckYourEligibility.API.Boundary.Responses;

public class FosterCarerRequest
{
    private string? _carerNationalInsuranceNumber;

    public string CarerFirstName { get; set; }

    public string CarerLastName { get; set; }

    public DateTime CarerDateOfBirth { get; set; }

    public int? LocalAuthorityID { get; set; }

    public bool HasPartner { get; set; }

    public FosterCarerRequest()
    {

    }

    public FosterCarerRequest(FosterCarer fosterCarer)
    {
        CarerFirstName = fosterCarer.FirstName;
        CarerLastName = fosterCarer.LastName;
        CarerDateOfBirth = fosterCarer.DateOfBirth;
        CarerNationalInsuranceNumber = fosterCarer.NationalInsuranceNumber;
        LocalAuthorityID = fosterCarer.LocalAuthorityID;
        HasPartner = fosterCarer.HasPartner;
    }

    public FosterCarerRequest(FosterFamilyResponse fosterCarer)
    {
        CarerFirstName = fosterCarer.CarerFirstName;
        CarerLastName = fosterCarer.CarerLastName;
        CarerDateOfBirth = fosterCarer.CarerDateOfBirth;
        CarerNationalInsuranceNumber = fosterCarer.CarerNationalInsuranceNumber;
        LocalAuthorityID = fosterCarer.LocalAuthorityID;
        HasPartner = fosterCarer.HasPartner;
    }

    public string? CarerNationalInsuranceNumber
    {
        get => _carerNationalInsuranceNumber;
        set => _carerNationalInsuranceNumber = value;
    }

}