using CheckYourEligibility.API.Boundary.Responses;

public class FosterChildRequest
{

    public FosterChildRequest()
    {

    }

    public FosterChildRequest(FosterChild fosterChild)
    {
        ChildFirstName = fosterChild.FirstName;
        ChildLastName = fosterChild.LastName;
        ChildDateOfBirth = fosterChild.DateOfBirth;
        ChildPostCode = fosterChild.PostCode;
    }

    public FosterChildRequest(FosterChildResponse fosterChild)
    {
        ChildFirstName = fosterChild.ChildFirstName;
        ChildLastName = fosterChild.ChildLastName;
        ChildDateOfBirth = fosterChild.ChildDateOfBirth;
        ChildPostCode = fosterChild.ChildPostCode;
    }

    public string ChildFirstName { get; set; }
    
    public string ChildLastName { get; set; }

    public DateTime ChildDateOfBirth { get; set; }

    public string ChildPostCode { get; set; }

}