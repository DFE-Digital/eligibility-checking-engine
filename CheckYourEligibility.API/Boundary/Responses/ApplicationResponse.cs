namespace CheckYourEligibility.API.Boundary.Responses;

public class ApplicationResponse
{
    public string Id { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public ApplicationEstablishment Establishment { get; set; }
    public string ParentFirstName { get; set; } = string.Empty  ;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string? ParentNationalInsuranceNumber { get; set; }
    public string? ParentNationalAsylumSeekerServiceNumber { get; set; }
    public string ParentDateOfBirth { get; set; } = string.Empty;
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public string ChildDateOfBirth { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } 
    public DateTime Created { get; set; }

    public ApplicationHash? CheckOutcome { get; set; }

    public class ApplicationEstablishment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public EstablishmentLocalAuthority LocalAuthority { get; set; }

        public class EstablishmentLocalAuthority
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }

    public class ApplicationUser
    {
        public string UserID { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;
    }   

    public class ApplicationHash
    {
        public string? Outcome { get; set; }
    }
}