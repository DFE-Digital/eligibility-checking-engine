import {
  invalidPostEligibilityReportRequest,
  validLoginRequestBody,
  validPostEligibilityReportRequest,
} from "../../support/requestBodies";
import { getandVerifyBearerToken } from "../../support/apiHelpers";

describe("Post Eligibility Report - Valid Requests", () => {
  const eligibilityReportRequest = validPostEligibilityReportRequest();
  it("Verify 200 Accepted response is returned with valid data", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "check-eligibility/report",
          eligibilityReportRequest,
          token,
        ).then((response) => {
          //
          cy.verifyApiResponseCode(response, 200);
          // Assert the response body data

          // check first element in the response array
          cy.verifyPostEligibilityReportResponse(response);
        });
      },
    );
  });
});

// If request has bad data, such invalid dates, local auth code etc
describe("Post Eligibility Report - Invalid Requests", () => {
  const invalidEligibilityReportRequest = invalidPostEligibilityReportRequest();
  it("Verify 400 Bad Request response is returned with invalid data", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "check-eligibility/report",
          invalidEligibilityReportRequest,
          token,
        ).then((response) => {
          // bad request response code
          cy.verifyApiResponseCode(response, 400);
        });
      },
    );
  });
});
