using CheckYourEligibility.API.Domain.Enums;

namespace CheckYourEligibility.API.Boundary.Requests;

public interface ICheckEligibilityBulkRequest
{
    List<CheckEligibilityRequestData>? Data { get; set; }
}
public static class EligibilityBulkModelFactory
{
    public static CheckEligibilityRequestBulk CreateFromGeneric(CheckEligibilityRequestBulk model, CheckEligibilityType routeType)
    {
        foreach (var item in model.Data)
        {
            if (item.Type != routeType)
                item.Type = routeType;
        }

        return model;
    }
}

public class CheckEligibilityRequestBulk : ICheckEligibilityBulkRequest
{
    public List<CheckEligibilityRequestData>? Data { get; set; }
}
