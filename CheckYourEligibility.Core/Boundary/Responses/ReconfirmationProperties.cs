
using CheckYourEligibility.Core.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class ReconfirmationProperties
{

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public ReconfirmationStatus Status { get; set; }

}