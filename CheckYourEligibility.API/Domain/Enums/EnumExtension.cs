// Ignore Spelling: Fsm

using System.ComponentModel;

namespace CheckYourEligibility.API.Domain.Enums;

/// <summary>
///     This is the extension method that can be used anywhere once it is added with a using statement
/// </summary>
public static class EnumExtension
{
    //Extension Method
    /// <summary>
    ///     Returns the description attribute of the enum value
    /// </summary>
    /// <param name="value"> Enum to get the description for</param>
    /// <returns> Returns description</returns>
    public static string GetDescription(this Enum value)
    {
        if (value == null) return string.Empty;

        var fieldInfo = value.GetType().GetField(value.ToString());
        DescriptionAttribute[] attributes =
            (DescriptionAttribute[])fieldInfo!.GetCustomAttributes(typeof(DescriptionAttribute), false);

        if (attributes.Length > 0) return attributes[0].Description;

        return value.ToString();
    }
}