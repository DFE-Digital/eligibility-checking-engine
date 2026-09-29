using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.API.Boundary.Responses;

public class ReconfirmationProperties
{

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public ReconfirmationStatus Status { get; set; }

}