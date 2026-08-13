using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Services
{

    public interface IEligiblityCheckDataResponseMapper {
        CheckEligibilityItem MapCheckDataToResponseStandard(EligibilityCheck eligibilityCheck);
        CheckEligibilityItemBase MapCheckDataToResponse(EligibilityCheck eligibilityCheck);
        CheckEligibilityWorkingFamiliesItem MapCheckDataToResponseWorkingFamilies(EligibilityCheck eligibilityCheck);
    }
    /// <summary>
    /// Used by GetEligibilityCheck type Usecases
    /// </summary>
    public class EligiblityCheckDataResponseMapper : IEligiblityCheckDataResponseMapper
    {
        public EligiblityCheckDataResponseMapper(ILogger<EligiblityCheckDataResponseMapper> logger, ICheckEligibility checkGateway) {

                 
        }
        public CheckEligibilityItemBase MapCheckDataToResponse (EligibilityCheck eligibilityCheck)
        {
            return eligibilityCheck.Type switch
            {
                CheckEligibilityType.WorkingFamilies =>
                    MapCheckDataToResponseWorkingFamilies(eligibilityCheck),

                _ =>
                    MapCheckDataToResponseStandard(eligibilityCheck)
            };
        }
        public CheckEligibilityItem MapCheckDataToResponseStandard (
                EligibilityCheck eligibilityCheck)
            {

                var checkData = MapCheckDataBasedOnType(
                    eligibilityCheck.Type,
                    eligibilityCheck.CheckData);

                    var item = new CheckEligibilityItem();
                    item.Status = eligibilityCheck.Status.ToString();
                    item.Created = eligibilityCheck.Created;
                    item.ClientIdentifier = checkData?.ClientIdentifier;                
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

        public CheckEligibilityWorkingFamiliesItem MapCheckDataToResponseWorkingFamilies(EligibilityCheck eligibilityCheck) {

            var checkData = MapCheckDataBasedOnType(
                    CheckEligibilityType.WorkingFamilies,
                     eligibilityCheck.CheckData);
                    var item = new CheckEligibilityWorkingFamiliesItem();

            item.Created = eligibilityCheck.Created;
            item.Status = eligibilityCheck.Status.ToString();
            item.EligibilityCode = checkData.EligibilityCode;
            item.LastName = checkData.LastName;
            item.ValidityStartDate = checkData.ValidityStartDate;
            item.ValidityEndDate = checkData.ValidityEndDate;
            item.GracePeriodEndDate = checkData.GracePeriodEndDate;
            item.NationalInsuranceNumber = checkData.NationalInsuranceNumber;
            item.DateOfBirth = checkData.DateOfBirth;

            if (eligibilityCheck.BulkCheckID != null)
                item.EligibilityCheckID = eligibilityCheck.EligibilityCheckID;


            return item;

            }
# region Private
        private CheckProcessData MapCheckDataBasedOnType(CheckEligibilityType type, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return new CheckProcessData { Type = type };
            }

            switch (type)
            {
                case CheckEligibilityType.WorkingFamilies:
                    {
                        var checkItem = JsonConvert.DeserializeObject<CheckEligibilityRequestWorkingFamiliesBulkData>(data);

                        return new CheckProcessData
                        {
                            EligibilityCode = checkItem?.EligibilityCode,
                            NationalInsuranceNumber = checkItem?.NationalInsuranceNumber,
                            ValidityStartDate = checkItem?.ValidityStartDate,
                            ValidityEndDate = checkItem?.ValidityEndDate,
                            GracePeriodEndDate = checkItem?.GracePeriodEndDate,
                            LastName = checkItem?.LastName?.ToUpper(),
                            DateOfBirth = checkItem?.DateOfBirth,
                            ClientIdentifier = checkItem?.ClientIdentifier,
                            Type = type
                        };
                    }

                case CheckEligibilityType.FreeSchoolMeals:
                case CheckEligibilityType.TwoYearOffer:
                case CheckEligibilityType.EarlyYearPupilPremium:
                    {
                        var checkItem = JsonConvert.DeserializeObject<CheckEligibilityRequestBulkData>(data);

                        return new CheckProcessData
                        {
                            DateOfBirth = checkItem?.DateOfBirth,
                            LastName = checkItem?.LastName?.ToUpper(),
                            FirstName = checkItem?.FirstName,
                            ChildFirstName = checkItem?.ChildFirstName,
                            ChildLastName = checkItem?.ChildLastName,
                            ChildDateOfBirth = checkItem?.ChildDateOfBirth,
                            ChildSchoolURN = checkItem?.ChildSchoolURN,
                            EmailAddress = checkItem?.EmailAddress,
                            NationalAsylumSeekerServiceNumber = checkItem?.NationalAsylumSeekerServiceNumber,
                            NationalInsuranceNumber = checkItem?.NationalInsuranceNumber,
                            Type = type,
                            ClientIdentifier = checkItem?.ClientIdentifier,
                            EligibilityEndDate = checkItem?.EligibilityEndDate
                        };
                    }

                default:
                    throw new NotImplementedException($"Type:-{type} not supported.");
            }
        }
#endregion

    }
}