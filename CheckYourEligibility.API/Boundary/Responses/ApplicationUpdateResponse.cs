// Ignore Spelling: Fsm

namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationUpdateResponse
{
    public ApplicationUpdateDataResponse Data { get; set; }
}

public class ApplicationUpdateDataResponse
{
    public string Status { get; set; }
    public int? EstablishmentUrn { get; set; }
}
