using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using DocumentFormat.OpenXml.InkML;

namespace CheckYourEligibility.API.Helpers
{
    /// <summary>
    /// Helper classes to help calcualte the following for a working family check
    /// 1. Is Discretionary validity start date applied
    /// 2. Calculation of term validity
    /// 3. Calcuation of Reconfirmation properties
    /// 4. Set eligibility code type - Temporary,Permanent, Foster
    /// </summary>
    public class WorkingFamiliesCheckHelper
    {

        public class Term
        {
           public TermName Name { get; set; }
            public DateTime StartDate { get; set; }
            public Term(TermName name, DateTime startDate) {
                 Name = name;
                 StartDate = startDate;
       
            }
        }
        /// <summary>
        /// if VSD == DVSD it means that DVSD logic has not been applied during the event import for this code
        /// return null if not applicable
        /// </summary>
        /// <param name="validityStartDate"></param>
        /// <param name="discretionaryValidityStartDate"></param>
        /// <returns></returns>
        public static bool? IsDiscretionaryValidityStartDateApplied(string validityStartDate, string discretionaryValidityStartDate) {

            if (DateTime.TryParse(validityStartDate, out var vsd) && DateTime.TryParse(discretionaryValidityStartDate, out var dvsd)) {
                if (vsd == dvsd) return false;
                return true;
            }
            return null;

            
        }
        /// <summary>
        /// Calculates the terms for which a code is valid.
        /// Returns:
        /// - [] when the child is too old or the code has expired.
        /// - [NextTerm] when the child is too young or the VSD falls  within the current term.
        /// - [CurrentTerm, NextTerm] when the GPED  extends beyond the start of the next term.
        /// - [CurrentTerm] when GPED does not extend beyond the start of the next term,
        /// VSD is before the start of the current term,assuming child is correct age 
        /// </summary>
        public static TermValidity SetTermValidity(DateTime checkDate, string gracePeriodEndDAte, string validityStartDate, string childDOB)
        {

            if (DateTime.TryParse(gracePeriodEndDAte, out var gpd) && DateTime.TryParse(validityStartDate, out var vsd) && DateTime.TryParse(childDOB, out var dob)) {


                (Term current, Term next) = GetTerms(checkDate);
                if (ChildIsTooYoung(dob, checkDate) ||
                    ChildIsTooOld(dob, checkDate) ||
                    checkDate > gpd) return new TermValidity(TermName.None, TermName.None);

                if (vsd >= current.StartDate) { return new TermValidity(TermName.None, next.Name); }

                if (gpd > next.StartDate) { return new TermValidity(current.Name, next.Name); }

                return new TermValidity(TermName.None, current.Name);
            }
            return new TermValidity(null, null);

        }
        public static EligibilityCodeType GetEligibilityCodeType(string eligibilityCode) { 
        
            if (eligibilityCode.StartsWith("1")) return EligibilityCodeType.Temporary;
            if (eligibilityCode.StartsWith("4")) return EligibilityCodeType.Foster;
            return EligibilityCodeType.Standard;
        }
        public static ReconfirmationProperties SetReconfirmationProperties(string validityEndDate,string gracePeriodEndDate, DateTime checkDate, EligibilityCodeType? codeType, string childDOB)
        {
            if (DateTime.TryParse(gracePeriodEndDate, out var gpd) && DateTime.TryParse(validityEndDate, out var ved) && DateTime.TryParse(childDOB, out var dob)) {

                

                    if (codeType == EligibilityCodeType.Temporary)
                    {
                        return new ReconfirmationProperties();
                    }
                    else if (ChildIsTooOld(dob, checkDate)) //child too old - Child has reached compulsory school age

                        return new ReconfirmationProperties()
                        {
                            Status = ReconfirmationStatus.ChildTooOld
                        };

                    DateTime startReconfirmDate = ved.AddDays(-28);
                    ReconfirmationProperties reconfirmationProperties = new ReconfirmationProperties();

                    if (checkDate > ved)
                    {

                        reconfirmationProperties.Status = ReconfirmationStatus.Overdue;
                    }

                    else if (checkDate < startReconfirmDate)
                    {
                        reconfirmationProperties.Status = ReconfirmationStatus.NotDueYet;
                    }
                    else { reconfirmationProperties.Status = ReconfirmationStatus.Due; }

                    reconfirmationProperties.StartDate = startReconfirmDate;
                    reconfirmationProperties.EndDate = ved;

                    return reconfirmationProperties;
                }

            return new ReconfirmationProperties();
          
        }
#region Private

        private static (Term Current, Term Next) GetTerms(DateTime date)
        {
            int year = date.Year;

            if (date >= new DateTime(year, 9, 1))
            {
                return (
                    new Term(TermName.Autumn, new DateTime(year, 9, 1)),
                    new Term(TermName.Spring, new DateTime(year + 1, 1, 1))
                );
            }

            if (date >= new DateTime(year, 4, 1))
            {
                return (
                    new Term(TermName.Summer, new DateTime(year, 4, 1)),
                    new Term(TermName.Autumn, new DateTime(year, 9, 1))
                );
            }

            return (
                new Term(TermName.Spring, new DateTime(year, 1, 1)),
                new Term(TermName.Summer, new DateTime(year, 4, 1))
            );
        }
        /// <summary>
        ///  Caclculates if child turns 9 months after the start of the current turm => child is too young
        /// </summary>
        /// <param name="dateOfBirth"></param>
        /// <param name="checkDate"></param>
        /// <returns></returns>
        private static bool ChildIsTooYoung(DateTime dateOfBirth, DateTime checkDate) {

            DateTime nineMonthsOld = dateOfBirth.AddMonths(9);
            var (currentTerm, _) = GetTerms(checkDate);          
            return nineMonthsOld > currentTerm.StartDate;       
        
        }

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
