using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using System.Net.NetworkInformation;
using System.Reflection.Metadata.Ecma335;

namespace CheckYourEligibility.API.Extensions.WorkingFamilies
{
    public static class CheckExtensions
    {
        public static EligibilityCodeType GetEligibilityCodeType(this string code) =>
            code switch
            {
                var c when c.StartsWith("1") => EligibilityCodeType.Temporary,
                var c when c.StartsWith("4") => EligibilityCodeType.Foster,
                _ => EligibilityCodeType.Standard
            };
    }
}