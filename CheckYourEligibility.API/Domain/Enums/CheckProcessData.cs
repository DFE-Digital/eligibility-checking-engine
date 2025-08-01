﻿using System.Text;
using System.Security.Cryptography;
using CheckYourEligibility.API.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace CheckYourEligibility.API.Gateways;

public class CheckProcessData
{
    public string? NationalInsuranceNumber { get; set; }

    public string LastName { get; set; }

    public string EligibilityCode { get; set; }
    public string ValidityStartDate { get; set; }
    public string ValidityEndDate { get; set; }
    public string GracePeriodEndDate { get; set; }
    public string DateOfBirth { get; set; }
    public string SubmissionDate { get; set; }
    public string? NationalAsylumSeekerServiceNumber { get; set; }

    public string? ClientIdentifier { get; set; }

    public CheckEligibilityType Type { get; set; }

    /// <summary>
    ///     Get hash identifier for type
    /// </summary>
    /// <returns>String of hashed values</returns>
    public string GetHash()
    {
        string input = $"""
            {LastName?.ToUpper()}
            {(NationalInsuranceNumber.IsNullOrEmpty() ?
                NationalAsylumSeekerServiceNumber?.ToUpper() : NationalInsuranceNumber?.ToUpper())}
            {DateOfBirth}
            {Type}
            """;
        switch (this.Type) {
            case CheckEligibilityType.WorkingFamilies:
               input += $"""           
            {EligibilityCode}
            {GracePeriodEndDate}
            {ValidityStartDate}
            {ValidityEndDate}
            {SubmissionDate}
            """;
                break;        
        }

     
        var inputBytes = Encoding.UTF8.GetBytes(input.Replace(Environment.NewLine, ""));
        var inputHash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(inputHash);
    }
}