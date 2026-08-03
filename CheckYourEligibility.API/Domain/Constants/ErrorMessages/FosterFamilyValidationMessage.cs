// Ignore Spelling: FSM

namespace CheckYourEligibility.API.Domain.Constants.ErrorMessages;

public static class FosterFamilyValidationMessages
{
    public const string FosterCarerId = "A valid fosterCarerId is required";
    public const string FosterChildId = "A valid fosterChildId is required";
    public const string LocalAuthorityId = "Local Authority ID is required";
    public const string CreateFosterChildPermission = "You do not have permission to create a foster child for this Local Authority";
    public const string CreateFosterCarerPermission = "You do not have permission to create a foster carer for this Local Authority";
    public const string CreateFosterFamilyPermission = "You do not have permission to create a foster family for this Local Authority";
    public const string GetFosterChildPermission = "You do not have permission to get a foster child for this Local Authority";
    public const string GetFosterCarerPermission = "You do not have permission to get a foster carer for this Local Authority";
    public const string GetFosterFamilyPermission = "You do not have permission to get a foster family for this Local Authority";
    public const string UpdateFosterChildPermission = "You do not have permission to update a foster child for this Local Authority";
    public const string UpdateFosterCarerPermission = "You do not have permission to update a foster carer for this Local Authority";
    public const string UpdateFosterFamilyPermission = "You do not have permission to update a foster family for this Local Authority";
    public const string SearchFosterFamiliesPermission = "You do not have permission to search foster families for this Local Authority";
}