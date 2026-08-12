public class FosterCarerRequest
{
    public string CarerFirstName { get; set; }
    public string CarerLastName { get; set; }
    public DateTime CarerDateOfBirth { get; set; }
    public int? LocalAuthorityID { get; set; }
    private string? _carerNationalInsuranceNumber;
    public string? CarerNationalInsuranceNumber
    {
        get => _carerNationalInsuranceNumber;
        set => _carerNationalInsuranceNumber =
            value?.ToUpper().Replace(" ", string.Empty);
    }
}