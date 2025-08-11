// Ignore Spelling: FSM

namespace CheckYourEligibility.API.Domain.Constants.ErrorMessages;

public static class ValidationMessages
{
    public const string NI_and_NASS =
        "National Insurance Number or National Asylum Seeker Service Number is required is required, not both";

    public const string DOB = "Date of birth is required:- (yyyy-mm-dd)";
    public const string ChildDOB = "Child Date of birth is required:- (yyyy-mm-dd)";
    public const string LastName = "LastName is required";
    public const string LastNameWf = "LastName contains an invalid character";
    public const string FirstName = "FirstName is required";
    public const string ChildLastName = "Child LastName is required";
    public const string ChildFirstName = "Child FirstName is required";
    public const string NI = "Invalid National Insurance Number";
    public const string NI_or_NASS = "National Insurance Number or National Asylum Seeker Service Number is required";
    public const string EligibilityCode = "Eligibility code must be 11 digits long";
}