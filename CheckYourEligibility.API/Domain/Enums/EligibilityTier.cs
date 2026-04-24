namespace CheckYourEligibility.API.Domain.Enums
{
    /// <summary>
    /// To support new FSM policy
    /// where everyone on UC is Eligible to receive FSM
    /// </summary>
    public enum EligibilityTier
    {
        /// <summary>
        /// Check is below the threshold
        /// </summary>
        Targeted,
        /// <summary>
        /// Check is above the threshold
        /// </summary>
        Expanded
    }
}