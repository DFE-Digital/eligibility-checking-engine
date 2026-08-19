using CheckYourEligibility.API.Boundary.Requests;
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
    }
}
