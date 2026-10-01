using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.API.Helpers
{
    /// <summary>
    /// Helper classes to help calcualte the following for a working family check
    /// 1. Is Discretionary validity start date applied
    /// 2. Calculation of term validity
    /// 3. Calcuation of Reconfirmation properties
    /// 4. Set eligibility code type - Temporary,Permanent, Foster
    /// </summary>
    public static class WorkingFamiliesCheckHelper
    {

        /// <summary>
        /// if VSD == DVSD it means that DVSD logic has not been applied during the event import for this code
        /// return null if not applicable
        /// </summary>
        /// <param name="validityStartDate"></param>
        /// <param name="discretionaryValidityStartDate"></param>
        /// <returns></returns>
        public static bool? IsDiscretionaryValidityStartDateApplied(string validityStartDate, string discretionaryValidityStartDate)
        {

            if (DateTime.TryParse(validityStartDate, out var vsd) && DateTime.TryParse(discretionaryValidityStartDate, out var dvsd))
            {
                if (vsd == dvsd) return false;
                return true;
            }
            return null;


        }
        /// <summary>
        /// Calculates the terms for which a code is valid.
        /// Returns:
        /// - [] when the child is too old or the code has expired, or GPED is not applied(only one event exists that has never been valid).
        /// - [NextTerm] when the child is too young or the VSD falls  within the current term.
        /// - [CurrentTerm, NextTerm] when the GPED  extends beyond the start of the next term.
        /// - [CurrentTerm] when GPED does not extend beyond the start of the next term,
        /// VSD is before the start of the current term,assuming child is correct age 
        /// </summary>
        public static TermValidity SetTermValidity(DateTime checkDate, string gracePeriodEndDAte, string validityStartDate, string childDOB, bool isGracePeriodEndDateApplied)
        {

            if (DateTime.TryParse(gracePeriodEndDAte, out var gpd) && DateTime.TryParse(validityStartDate, out var vsd) && DateTime.TryParse(childDOB, out var dob))
            {
                (Term current, Term next) = GetTerms(checkDate);
             
                if (ChildIsTooOld(dob, checkDate) || checkDate > gpd || !isGracePeriodEndDateApplied)
                {
                    return new TermValidity(Term.None, Term.None);
                }
                if (ChildIsTooYoung(dob, checkDate) && vsd < dob.AddMonths(9))
                {
                    vsd = dob.AddMonths(9);
                }

                if (vsd >= current.StartDate) { return new TermValidity(Term.None, next); }

                if (gpd > next.StartDate) { return new TermValidity(current, next); }

                return new TermValidity(current, Term.None);
            }
            return new TermValidity(null, null);

        }
        public static EligibilityCodeType GetEligibilityCodeType(string eligibilityCode)
        {

            if (eligibilityCode.StartsWith("1")) return EligibilityCodeType.Temporary;
            if (eligibilityCode.StartsWith("4")) return EligibilityCodeType.Foster;
            return EligibilityCodeType.Standard;

        }
        public static EligibilityCodeType GetTestEligibilityCodeType(string eligibilityCode)
        {

            if (eligibilityCode.EndsWith("1")) return EligibilityCodeType.Temporary;
            if (eligibilityCode.EndsWith("4")) return EligibilityCodeType.Foster;
            return EligibilityCodeType.Standard;

        }
        /// <summary>
        /// Calculates reconfirmation window and reconfirmation status
        /// </summary>
        /// <param name="validityEndDate"></param>
        /// <param name="gracePeriodEndDate"></param>
        /// <param name="checkDate"></param>
        /// <param name="codeType"></param>
        /// <param name="childDOB"></param>
        /// <returns></returns>
        public static ReconfirmationProperties SetReconfirmationProperties(string validityEndDate, string gracePeriodEndDate, DateTime checkDate, EligibilityCodeType? codeType, string childDOB)
        {
            if (DateTime.TryParse(gracePeriodEndDate, out var gpd) && DateTime.TryParse(validityEndDate, out var ved) && DateTime.TryParse(childDOB, out var dob))
            {
                if (ChildIsTooOld(dob, checkDate))
                { //child too old - Child has reached compulsory school age

                    return new ReconfirmationProperties()
                    {
                        Status = ReconfirmationStatus.ChildTooOld
                    };
                }
                if (codeType == EligibilityCodeType.Temporary)
                {
                    return new ReconfirmationProperties()
                    {
                        Status = ReconfirmationStatus.NotApplicable
                    };
                }
                DateTime startReconfirmDate = ved.AddDays(-28);
                ReconfirmationProperties reconfirmationProperties = new ReconfirmationProperties();

                if (checkDate.Date > ved.Date)
                {

                    reconfirmationProperties.Status = ReconfirmationStatus.Overdue;
                }

                else if (checkDate.Date < startReconfirmDate.Date)
                {
                    reconfirmationProperties.Status = ReconfirmationStatus.NotDueYet;
                }
                else { reconfirmationProperties.Status = ReconfirmationStatus.Due; }

                reconfirmationProperties.StartDate = startReconfirmDate;
                reconfirmationProperties.EndDate = ved;

                return reconfirmationProperties;
            }

            return new ReconfirmationProperties()
            {
                Status = ReconfirmationStatus.NotApplicable
            };

        }

        // Spring - 1st of Jan - 31st of March(89-90 days)
        // Summer - 1st of Apr - 31st of Aug(153 days)
        // Autumn - 1st of Sept - 31st of Dec(122 days)
        public static (Term Current, Term Next) GetTerms(DateTime date)
        {
            int year = date.Year;

            if (date >= new DateTime(year, 9, 1))
            {
                return (
                    new Term(TermName.Autumn, new DateTime(year, 9, 1), new DateTime(year, 12, 31)),
                    new Term(TermName.Spring, new DateTime(year + 1, 1, 1), new DateTime(year + 1, 3, 31))
                );
            }

            if (date >= new DateTime(year, 4, 1))
            {
                return (
                    new Term(TermName.Summer, new DateTime(year, 4, 1), new DateTime(year, 8, 31)),
                    new Term(TermName.Autumn, new DateTime(year, 9, 1), new DateTime(year, 12, 31))
                );
            }

            return (
                new Term(TermName.Spring, new DateTime(year, 1, 1), new DateTime(year, 3, 31)),
                new Term(TermName.Summer, new DateTime(year, 4, 1), new DateTime(year, 8, 31))
            );
        }
        /// <summary>
        ///  Calculates if child turns 9 months after the start of the current term => child is too young
        /// </summary>
        /// <param name="dateOfBirth"></param>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        public static bool ChildIsTooYoung(DateTime dateOfBirth, DateTime checkDate)
        {
            DateTime nineMonthsOld = dateOfBirth.AddMonths(9);
            var (currentTerm, _) = GetTerms(checkDate);
            return nineMonthsOld > currentTerm.StartDate;
        }
        /// <summary>
        /// Determine if GPED is applied
        /// </summary>
        /// <param name="validityStartDate"></param>
        /// <param name="validityEndDdate"></param>
        /// <returns></returns>
        public static bool isGracePeriodEndDateApplied(DateTime validityStartDate, DateTime validityEndDdate, int eventCount) {
            var currentTerm = GetTerms(DateTime.UtcNow.Date).Current;

            if (eventCount > 1) { return true; }

            if (validityStartDate >= currentTerm.StartDate && validityEndDdate <= currentTerm.EndDate) {
               
                return false;
            }
                return true; 
        }

        /// <summary>
        /// Determines if the contiguity of an event is broken:
        /// If only one event is found it - exist early and return event
        /// If only two events are found and the reconfirmation(latest event submission date) has happened after the historicEvent VED
        /// and the earlier record VSD and VED fall within the same term (code has never been valid).
        /// or if more than more events exist and the reconfirmation(latest event submission date) has happened after the historicEvent GPED
        /// </summary>
        public static (WorkingFamiliesEvent,bool) CalculateContiguousChainForCodeFromEvents(List<WorkingFamiliesEvent> eventRecords) {

            var latestEvent = eventRecords.FirstOrDefault();
            bool gracePeriodEndDateApplied = isGracePeriodEndDateApplied(latestEvent.ValidityStartDate, latestEvent.ValidityEndDate, eventRecords.Count);
            
            if (eventRecords.Count == 1) { 
                
                return (latestEvent, gracePeriodEndDateApplied);
            }

            // chain broken
            // earlier record is considerted expired
            // return latest record
            var prevoiusEvent = eventRecords[1];
            var historicalEventVSDTerm = GetTerms(prevoiusEvent.DiscretionaryValidityStartDate);
            var historicalEventVEDTerm = GetTerms(prevoiusEvent.ValidityEndDate);

            if ((eventRecords.Count == 2 && latestEvent.SubmissionDate > prevoiusEvent.ValidityEndDate &&
                historicalEventVSDTerm.Current.Name == historicalEventVEDTerm.Current.Name) ||
                latestEvent.DiscretionaryValidityStartDate > prevoiusEvent.GracePeriodEndDate)
            {

                return (latestEvent, gracePeriodEndDateApplied);

            }
            else {

                //Check for contiguous events and set VSD to earliest VSD of the current contiguous block
                for (int i = 0; i < eventRecords.Count() - 1; i++)
                {
                    if (eventRecords[i].DiscretionaryValidityStartDate <= eventRecords[i + 1].GracePeriodEndDate)
                    {
                        latestEvent.DiscretionaryValidityStartDate = eventRecords[i + 1].DiscretionaryValidityStartDate;
                        latestEvent.ValidityStartDate = eventRecords[i + 1].ValidityStartDate;
                    }
                    else
                    {
                        break;
                    }
                }
                return (latestEvent, gracePeriodEndDateApplied);

            }

        }
        /// <summary>
        /// Determines whether a Working Families code is eligible based on the source
        /// of the request and the applicable validity periods.
        /// Internal site requests ("childcare-admin") use the validity end date when
        /// GracePeriodEndDateApplied is FALSE, else it uses the grace period end date.
        /// All other requests use the grace period end date to determine eligibility.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="discretionaryValidityStartDate"></param>
        /// <param name="validityEndDate"></param>
        /// <param name="gracePeriodEnDate"></param>
        /// <param name="GracePeriodEndDateApplied"></param>
        /// <returns></returns>
        public static CheckEligibilityStatus DetermineWorkingFamiliesCodeEligibility(string source, DateTime discretionaryValidityStartDate, DateTime validityEndDate, DateTime? gracePeriodEnDate, bool GracePeriodEndDateApplied) {

            DateTime today  = DateTime.UtcNow.Date;

            switch (source) {
                //internal site
                case "childcare-admin":
                    if (GracePeriodEndDateApplied)
                    {
                        goto default;
                    }
                    if (today >= discretionaryValidityStartDate && today <= validityEndDate) { 
                        return CheckEligibilityStatus.eligible;  
                    }
                    else return CheckEligibilityStatus.notEligible;
                //client site
                default:

                    if (today >= discretionaryValidityStartDate && today <= gracePeriodEnDate)
                    {
                        return CheckEligibilityStatus.eligible;
                    }
                    else return CheckEligibilityStatus.notEligible;
            }
        }
        #region Private
        /// <summary>
        /// Calculates if checkDate is on/after the start of this term => child is too old
        /// </summary>
        /// <param name="dateOfBirth"></param>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        private static bool ChildIsTooOld(DateTime dateOfBirth, DateTime checkDate)
        {
            DateTime fifthBirthday = dateOfBirth.AddYears(5);
            var (_, termAfterBirthday) = GetTerms(fifthBirthday);
            return checkDate >= termAfterBirthday.StartDate;
        }
        #endregion

    }
  
}
