namespace CheckYourEligibility.API.Helpers
{
    public static class EligibilityCheckHelper
    {
        /// <summary>
        /// Extract LA Id from scope if it exists
        /// else return an empty string.
        /// </summary>
        /// <param name="scope"></param>
        /// <returns></returns>
        public static string ExtractLAIdFromScope(string scope)
        {
            string laWithIdSyntax = "local_authority:";
            string laId = string.Empty;

            if (!string.IsNullOrEmpty(scope) && scope.Contains(laWithIdSyntax))
            {
                int LaIdStartIndex = scope.IndexOf(laWithIdSyntax) + laWithIdSyntax.Length;
                var LaIdendIndex = scope.IndexOf(" ", LaIdStartIndex);
                if (LaIdendIndex == -1)
                {
                    laId = scope.Substring(LaIdStartIndex).Trim();
                }
                else
                {
                    laId = scope.Substring(LaIdStartIndex, LaIdendIndex - LaIdStartIndex).Trim();
                }

            }
            return laId;
        }

        /// <summary>
        ///  Calculate EligibilityEndDate for FSM
        ///  If check is created before or on the 31st of may
        ///  then eligibilityEndDate = 31st of july the same year the check is created
        ///  If check is created before or after the 1st of June 
        ///  then eligibilityEndDate = 31st of July next year
        /// </summary>
        public static DateTime GetEligibilityEndDateFSM(DateTime createdDate)
        {

            DateTime eligibilityEndDate = new();

            if (createdDate <= new DateTime(createdDate.Year, 5, 31))
            {
                eligibilityEndDate = new DateTime(createdDate.Year, 7, 31);
            }
            if (createdDate >= new DateTime(createdDate.Year, 5, 31))
            {
                eligibilityEndDate = new DateTime(createdDate.Year + 1, 7, 31);
            }
            return eligibilityEndDate;
        }

    }
}