using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Helpers;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Services
{

    public interface IEligibilityCheckDataResponseMapper {

        CheckEligibilityItemBase MapCheckDataToResponse(EligibilityCheck eligibilityCheck);
        CheckEligibilityWorkingFamiliesItem MapCheckDataToResponseWorkingFamilies(EligibilityCheck eligibilityCheck, bool isInternal = false);
    }
    /// <summary>
    /// Used by GetEligibilityCheck type Usecases
    /// </summary>
    public class EligibilityCheckDataResponseMapper : IEligibilityCheckDataResponseMapper
    {
        public EligibilityCheckDataResponseMapper(ILogger<EligibilityCheckDataResponseMapper> logger, ICheckEligibility checkGateway) {

                 
        }
        public CheckEligibilityItemBase MapCheckDataToResponse(EligibilityCheck eligibilityCheck)
        {
            return eligibilityCheck.Type switch
            {
                CheckEligibilityType.WorkingFamilies =>
                    MapCheckDataToResponseWorkingFamilies(eligibilityCheck),
                _ =>
                    MapCheckDataToResponseStandard(eligibilityCheck)
            };
        }

        public CheckEligibilityWorkingFamiliesItem MapCheckDataToResponseWorkingFamilies(EligibilityCheck eligibilityCheck, bool isInternal = false) {

            var checkData = JsonConvert.DeserializeObject<CheckEligibilityRequestWorkingFamiliesBulkData>(eligibilityCheck.CheckData);

            var item = new CheckEligibilityWorkingFamiliesItem();

            item.Created = eligibilityCheck.Created;
            item.Status = eligibilityCheck.Status.ToString();
            item.EligibilityCode = checkData.EligibilityCode;
            item.LastName = checkData.LastName;
            // return DVSD as the absolute truth for client site checks
            // if old hash  of new made check still exists and DVSD is null fall back to VSD from old code
            // NOTE: once we remove hashing this conditional should be removed
            item.ValidityStartDate = checkData.DiscretionaryValidityStartDate ?? checkData.ValidityStartDate; 
            item.ValidityEndDate = checkData.ValidityEndDate;
            item.GracePeriodEndDate = checkData.GracePeriodEndDate;
            item.NationalInsuranceNumber = checkData.NationalInsuranceNumber;
            item.DateOfBirth = checkData.DateOfBirth;
            item.Order = checkData.Order;

            // for internal site endpoint provide both dates
            if (isInternal) {
                item.ValidityStartDate = checkData.ValidityStartDate;
                item.DiscretionaryValidityStartDate = checkData.DiscretionaryValidityStartDate;
            }
                
            if (eligibilityCheck.BulkCheckID != null)
                item.EligibilityCheckID = eligibilityCheck.EligibilityCheckID;

            return item;

            }

        #region Private
        private CheckEligibilityItem MapCheckDataToResponseStandard(
        EligibilityCheck eligibilityCheck)
        {

            var checkData = MapCheckDataHelper.MapCheckDataBasedOnType(
                eligibilityCheck.Type,
                eligibilityCheck.CheckData);

            var item = new CheckEligibilityItem();
            item.Status = eligibilityCheck.Status.ToString();
            item.Created = eligibilityCheck.Created;
            item.ClientIdentifier = checkData?.ClientIdentifier;
            item.Order = checkData?.Order;
            item.DateOfBirth = checkData.DateOfBirth;
            item.NationalInsuranceNumber = checkData.NationalInsuranceNumber;
            item.NationalAsylumSeekerServiceNumber =
            checkData.NationalAsylumSeekerServiceNumber;
            item.LastName = checkData.LastName;
            item.FirstName = checkData.FirstName;
            item.ChildFirstName = checkData.ChildFirstName;
            item.ChildLastName = checkData.ChildLastName;
            item.ChildDateOfBirth = checkData.ChildDateOfBirth;
            item.ChildSchoolURN = checkData.ChildSchoolURN;
            item.EligibilityEndDate = checkData.EligibilityEndDate;
            item.EmailAddress = checkData.EmailAddress;

            if (eligibilityCheck.BulkCheckID != null)
                item.EligibilityCheckID = eligibilityCheck.EligibilityCheckID;

            return item;
        }
        #endregion

    }
}