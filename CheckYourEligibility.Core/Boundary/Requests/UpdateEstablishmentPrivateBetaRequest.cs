namespace CheckYourEligibility.Core.Boundary.Requests
{
    public class UpdateEstablishmentPrivateBetaRequest
    {
        public int EstablishmentId { get; set; }
        public bool InPrivateBeta { get; set; }
    }
}
