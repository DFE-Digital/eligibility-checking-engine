namespace CheckYourEligibility.API.Domain
{
    public class WorkingFamiliesDualRunningChecks
    {
        public string EligibilityCheckId { get; set; }
        public string EligibilityCode { get; set; }
        public string ECEStatus { get; set; }
        public string ECSStatus { get; set; }
        public string ECSQualifier { get; set; }
        public string ECSValidityDates { get; set; }
        public string ECEValidityDates { get; set; }
        public string Created { get; set; }
        public virtual EligibilityCheck EligibilityCheck { get; set; }
    }
}