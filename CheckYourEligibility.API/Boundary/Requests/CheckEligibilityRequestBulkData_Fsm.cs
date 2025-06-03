using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public interface ICheckEligibilityBulkRequest<TItem>
{
    IEnumerable<TItem> Data { get; }
}


#region FreeSchoolMeals Type

public class CheckEligibilityRequestBulkData_Fsm : CheckEligibilityRequestData_Fsm
{
    public string? ClientIdentifier { get; set; }
}

public class CheckEligibilityRequestBulk_Fsm : ICheckEligibilityBulkRequest<CheckEligibilityRequestBulkData_Fsm>
{
    public IEnumerable<CheckEligibilityRequestBulkData_Fsm> Data { get; set; }
}

#endregion