namespace CheckYourEligibility.API.Helpers
{

    public static class FosterFamiliesHelper
    {
        public static FosterCarer BuildFosterCarer(
            FosterCarerRequest request,
            FosterPartnerRequest? partner,
            bool hasPartner)
        {
            return new FosterCarer
            {
                FosterCarerId = Guid.NewGuid(),

                FirstName = request.CarerFirstName,
                LastName = request.CarerLastName,
                DateOfBirth = request.CarerDateOfBirth,
                NationalInsuranceNumber = request.CarerNationalInsuranceNumber,
                LocalAuthorityID = request.LocalAuthorityID,

                HasPartner = hasPartner,

                PartnerFirstName = partner?.PartnerFirstName,
                PartnerLastName = partner?.PartnerLastName,
                PartnerDateOfBirth = partner?.PartnerDateOfBirth,
                PartnerNationalInsuranceNumber = partner?.PartnerNationalInsuranceNumber,

                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
        }

        public static FosterChild BuildFosterChild(
            FosterChildRequest request,
            DateTime submissionDate,
            Guid fosterCarerId)
        {
            return new FosterChild
            {
                FosterChildId = Guid.NewGuid(),

                FirstName = request.ChildFirstName,
                LastName = request.ChildLastName,
                DateOfBirth = request.ChildDateOfBirth,
                PostCode = request.ChildPostCode,

                FosterCarerId = fosterCarerId,

                SubmissionDate = submissionDate,

                Status = "Active",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
        }
    }
}