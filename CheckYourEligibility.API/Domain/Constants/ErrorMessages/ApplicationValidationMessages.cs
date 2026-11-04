// Ignore Spelling: FSM

namespace CheckYourEligibility.API.Domain.Constants.ErrorMessages;

public static class ValidationMessages
{
    public const string NI_and_NASS =
        "National Insurance Number or National Asylum Seeker Service Number is required is required, not both";

    public const string DOB = "DateOfBirth is required:- (yyyy-mm-dd)";
    public const string ChildDOB = "Child Date of birth is required:- (yyyy-mm-dd)";
    public const string LastName = "LastName is required";
    public const string LastNameWf = "LastName contains an invalid character";
    public const string FirstName = "FirstName is required";
    public const string ChildLastName = "Child LastName is required";
    public const string ChildFirstName = "Child FirstName is required";
    public const string NI = "Invalid National Insurance Number";
    public const string NASS = "Invalid National Asylum Seeker Service Number";
    public const string NI_or_NASS = "National Insurance Number or National Asylum Seeker Service Number is required";
    public const string SubmissionDate = "Submission date must not be in the future";
    public const string SubmissionDateOutOfRange = "Submission date must be within the last 31 days";
    public const string Email = "Invalid Email Address";
    public const string ReferenceTooLong = "Reference must be 100 characters or fewer.";

	//WF import validation messages
	public const string EligibilityCodeEmpty = "Enter an eligibility code that is 11 digits long";
	public const string EligibilityCodeLength = "Eligibility code must be 11 digits long";
    public const string EligibilityCodeInvalid = "Eligibility code must only contain numbers";
	public const string EligibilityCodeAlreadyExists = "Eligibility code has already been imported";
	public const string ValidityStartDateEmpty = "Enter a validity start date";
	public const string ValidityStartDateInvalid = "The validity start date must be in yyyy-mm-dd or dd-mm-yyyy format";
	public const string ValidityEndDateEmpty = "Enter a validity end date";
	public const string ValidityEndDateInvalid = "The validity end date must be in yyyy-mm-dd or dd-mm-yyyy format";
	public const string ParentNINumberEmpty = "Enter a National Insurance number";
	public const string ParentNINumberInvalid = "Enter a National Insurance number that is 2 letters, 6 numbers, then A, B, C or D, like QQ 12 34 56 C";
	public const string ParentDOBEmpty = "Enter parent or guardian's date of birth";
	public const string ParentDOBInvalid = "The parent date of birth must be in yyyy-mm-dd or dd-mm-yyyy format";
	public const string ParentFirstNameEmpty = "Enter parent's first name";
	public const string ParentLastNameEmpty = "Enter parent's last name";
	public const string ChildFirstNameEmpty = "Enter child's first name";
	public const string ChildLastNameEmpty = "Enter child's last name";
	public const string ChildDOBEmpty = "Enter child's date of birth";
	public const string ChildDOBInvalid = "The child date of birth must be in yyyy-mm-dd or dd-mm-yyyy format";
	public const string ChildPostCodeEmpty = "Enter postcode";
	public const string ChildPostCodeInvalid = "Enter a full UK postcode";
	public const string PartnerNINumberEmpty = "Enter a National Insurance number";
	public const string PartnerNINumberInvalid = "Enter a National Insurance number that is 2 letters, 6 numbers, then A, B, C or D, like QQ 12 34 56 C";
	public const string PartnerDOBEmpty = "Enter partner's date of birth";
	public const string PartnerDOBInvalid = "The partner date of birth must be in yyyy-mm-dd or dd-mm-yyyy format";
	public const string PartnerFirstNameEmpty = "Enter partner's first name";
	public const string PartnerLastNameEmpty = "Enter partner's last name";
	public const string SubmissionDateEmpty = "Enter a submitted on date";
	public const string SubmissionDateInFuture = "Submitted on date must be in the past";
	public const string SubmissionDateInvalid = "The submitted on date must be in yyyy-mm-dd or dd-mm-yyyy format";


}