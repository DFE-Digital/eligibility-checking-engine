using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Helpers
{
    public static class MapCheckDataHelper
    {
        public static CheckProcessData MapCheckDataBasedOnType(CheckEligibilityType type, string data)
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
                            DiscretionaryValidityStartDate = checkItem?.DiscretionaryValidityStartDate,
                            ValidityEndDate = checkItem?.ValidityEndDate,
                            GracePeriodEndDate = checkItem?.GracePeriodEndDate,
                            LastName = checkItem?.LastName?.ToUpper(),
                            DateOfBirth = checkItem?.DateOfBirth,
                            ClientIdentifier = checkItem?.ClientIdentifier,
                            Order = checkItem?.Order,
                            Reference = checkItem?.Reference,
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
                            Reference = checkItem?.Reference,
                            NationalAsylumSeekerServiceNumber = checkItem?.NationalAsylumSeekerServiceNumber,
                            NationalInsuranceNumber = checkItem?.NationalInsuranceNumber,
                            Type = type,
                            ClientIdentifier = checkItem?.ClientIdentifier,
                            EligibilityEndDate = checkItem?.EligibilityEndDate,
                            Order = checkItem?.Order
                        };
                    }

                default:
                    throw new NotImplementedException($"Type:-{type} not supported.");
            }
        }
        // map dates to check data from working family event when processing a check
        public static CheckProcessData BuildCheckProcessedData_WorkingFamilies(
            WorkingFamiliesEvent wfEvent,
            bool isGracePeriodEndDateApplied,
            CheckProcessData? checkData = null)
        {
            checkData ??= new CheckProcessData();
            checkData.DiscretionaryValidityStartDate = wfEvent.DiscretionaryValidityStartDate.ToString("yyyy-MM-dd");
            checkData.ValidityStartDate = wfEvent.ValidityStartDate.ToString("yyyy-MM-dd");
            checkData.ValidityEndDate = wfEvent.ValidityEndDate.ToString("yyyy-MM-dd");
            checkData.GracePeriodEndDate = wfEvent.GracePeriodEndDate.ToString("yyyy-MM-dd");
            checkData.LastName = wfEvent.ParentLastName;
            checkData.IsGracePeriodEndDateApplied = isGracePeriodEndDateApplied;

            return checkData;
        }
    }
}
