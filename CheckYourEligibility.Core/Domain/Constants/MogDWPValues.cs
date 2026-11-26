namespace CheckYourEligibility.Core.Domain.Constants;

public static class MogDWPValues
{
    public static string validCitizenSurnameEligible = "Smith";

    public static string validCitizenSurnameNotEligible = "Jones";
    public static string validCitizenSurnameDuplicatesFound = "MoqDuplicate";
    public static string validCitizenDob = "1990-01-01";
    public static string validCitizenNino = "AB123456C";

    public static string validCitizenNotEligibleGuid = "48ccfe37-7e43-4682-a412-19ec663ca551";
    public static string validCitizenEligibleGuid = "48ccfe37-7e43-4682-a412-19ec663ca552";
    public static string inValidCitizenGuid = "48ccfe37-7e43-4682-a412-19ec663ca553";
}