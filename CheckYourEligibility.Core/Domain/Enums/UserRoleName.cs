using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CheckYourEligibility.Core.Domain.Enums;

/// <summary>
///     Authorization roles
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum UserRoleName
{
    Support_CodeManagement,
    Support_UserManagement,
    Support_TestRole
}