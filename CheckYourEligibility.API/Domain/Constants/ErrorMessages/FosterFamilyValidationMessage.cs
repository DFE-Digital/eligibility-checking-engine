// Ignore Spelling: FSM

namespace CheckYourEligibility.API.Domain.Constants.ErrorMessages;

public static class FosterFamilyValidationMessages
{
    public const string FosterCarerId = "A valid fosterCarerId is required";
    public const string FosterChildId = "A valid fosterChildId is required";
    public const string NoLocalAuthorityScopeFound = "No local authority scope found";
    public const string InvalidPageNumber = "Invalid page number";
    public const string InvalidPageSize = "Invalid page size";
    public const string InvalidNINO = "Invalid NINO provided";

    //Carer
    public const string CarerFirstNameEmpty = "Enter carer's first name";
    public const string CarerFirstNameInvalid = "Carer's first name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string CarerLastNameEmpty = "Enter carer's last name";
    public const string CarerLastNameInvalid = "Carer's last name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string CarerNationalInsuranceNumberEmpty = "Enter a National Insurance number";
    public const string HasPartner = "Select yes if the carer has a partner";

    //Partner
    public const string PartnerIsRequired = "Partner details are required.";
    public const string PartnerFirstNameEmpty = "Enter partner's first name";
    public const string PartnerFirstNameInvalid = "Partner's first name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string PartnerLastNameEmpty = "Enter partner's last name";
    public const string PartnerLastNameInvalid = "Partner's last name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string PartnerNationalInsuranceNumberEmpty = "Enter a National Insurance number";

    //Child
    public const string ChildFirstNameEmpty = "Enter child's first name";
    public const string ChildFirstNameInvalid = "Child's first name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string ChildLastNameEmpty = "Enter child's last name";
    public const string ChildLastNameInvalid = "Child's last name must only include letters a to z, and special characters such as hyphens, spaces and apostrophes";
    public const string ChildPostCodeEmpty = "Enter postcode";
    public const string ChildPostCodeInvalid = "Enter a full UK postcode";
}
