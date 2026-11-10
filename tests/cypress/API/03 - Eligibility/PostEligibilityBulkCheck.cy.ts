import { getandVerifyBearerToken } from "../../support/apiHelpers";
import {
  invalidDateOfBirthBulkCheckRequestBody,
  validLoginRequestBodyTelford,
  validBulkRequestBody,
  validBulkRequestBodyLarge,
  invalidNinoBulkRequestBody,
  invalidNassBulkRequestBody,
  invalidLastNameBulkCheckRequestBody,
  invalidMultiChecksBulkRequestBody,
  validFSMRequest,
} from "../../support/requestBodies";

const endpoints = [
  "free-school-meals",
  "early-year-pupil-premium",
  "two-year-offer",
];
const endpointsSupportingNass = endpoints.filter(
  (endpoint) => endpoint !== "two-year-offer",
);
describe("Post Eligibility Bulk Check - Valid Requests", () => {
  const validBulkRequest = validBulkRequestBody();
  endpoints.forEach((endpoint) => {
    it(`Verify 202 Accepted response is returned with valid data for ${endpoint}`, () => {
      const requestBody =
        endpoint === "two-year-offer"
          ? { data: [validFSMRequest().data] }
          : validBulkRequest;

      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            requestBody,
            token,
          ).then((response) => {
            // Assert the status and statusText
            cy.verifyApiResponseCode(response, 202);
            // Assert the response body data
            cy.verifyPostEligibilityBulkCheckResponse(response);
          });
        },
      );
    });
  });

});

describe("Post Eligibility Bulk Check - Invalid Requests", () => {
  const invalidNinoBulkRequest = invalidNinoBulkRequestBody();
  const invalidNassBulkRequest = invalidNassBulkRequestBody();
  const invalidDobBulkRequest = invalidDateOfBirthBulkCheckRequestBody();
  const invalidLastNameBulkRequest = invalidLastNameBulkCheckRequestBody();
  const invalidMultiCheckBulkRequest = invalidMultiChecksBulkRequestBody();
  endpoints.forEach((endpoint) => {
    it(`Verify 400 Bad Request response is returned with invalid National Insurance number for ${endpoint}`, () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            invalidNinoBulkRequest,
            token,
          ).then((response) => {
            cy.verifyApiResponseCode(response, 400);
            expect(response.body.errors[0]).to.have.property(
              "title",
              "Enter a National Insurance number in the correct format",
            );
          });
        },
      );
    });
    it(`Verify 400 Bad Request response is returned with invalid date of birth for ${endpoint}`, () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            invalidDobBulkRequest,
            token,
          ).then((response) => {
            cy.verifyApiResponseCode(response, 400);
            expect(response.body.errors[0]).to.have.property(
              "title",
              "DateOfBirth is required:- (yyyy-mm-dd)",
            );
          });
        },
      );
    });
    it(`Verify 400 Bad Request response is returned with invalid last name for ${endpoint}`, () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            invalidLastNameBulkRequest,
            token,
          ).then((response) => {
            cy.verifyApiResponseCode(response, 400);
            expect(response.body.errors[0]).to.have.property(
              "title",
              "LastName is required",
            );
          });
        },
      );
    });
    it(`Verify 400 Bad Request response is returned with multiple invalid checks for ${endpoint}`, () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            invalidMultiCheckBulkRequest,
            token,
          ).then((response) => {
            cy.verifyApiResponseCode(response, 400);
            expect(response.body.errors[0]).to.have.property(
              "title",
              "LastName is required",
            );
            expect(response.body.errors[1]).to.have.property(
              "title",
              "Enter a National Insurance number in the correct format",
            );
          });
        },
      );
    });
  });
  endpointsSupportingNass.forEach((endpoint) => {
    it(`Verify 400 Bad Request response is returned with invalid National Asylum Seeker Service Number for ${endpoint}`, () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
        (token) => {
          cy.apiRequest(
            "POST",
            `bulk-check/${endpoint}`,
            invalidNassBulkRequest,
            token,
          ).then((response) => {
            cy.verifyApiResponseCode(response, 400);
            expect(response.body.errors[0]).to.have.property(
              "title",
              "Invalid National Asylum Seeker Service Number",
            );
          });
        },
      );
    });
  });
});
