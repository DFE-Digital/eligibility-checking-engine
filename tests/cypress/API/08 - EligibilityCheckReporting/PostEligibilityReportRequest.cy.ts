import {
  invalidPostEligibilityReportRequest,
  validLoginRequestBodyTelford,
  validPostEligibilityReportRequest,
} from "../../support/requestBodies";
import { getandVerifyBearerToken } from "../../support/apiHelpers";

describe("Post Eligibility Report - Valid Requests", () => {
  const eligibilityReportRequest = validPostEligibilityReportRequest();
  it("Verify 202 Accepted response is returned with valid data", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "check-eligibility/report",
          eligibilityReportRequest,
          token,
        ).then((response) => {
          //
          cy.verifyApiResponseCode(response, 202);
          // Assert the response body data

          // check first element in the response array
          cy.verifyPostEligibilityReportResponse(response);
        });
      },
    );
  });
});

// Report generation runs in the background, so poll the status endpoint until it finishes
describe("Post Eligibility Report - Background generation", () => {
  const maxAttempts = 30;
  const pollIntervalMs = 4000;

  const waitForReportToFinish = (
    reportId: string,
    token: string,
    attempt = 1,
  ): Cypress.Chainable<string> =>
    cy
      .apiRequest(
        "GET",
        `check-eligibility/report/${reportId}/status`,
        null,
        token,
      )
      .then((response) => {
        cy.verifyApiResponseCode(response, 200);
        const status: string = response.body.status;
        if (status === "Complete" || status === "Failed") {
          return cy.wrap(status);
        }
        expect(attempt, "report did not finish in time").to.be.lessThan(
          maxAttempts,
        );
        return cy
          .wait(pollIntervalMs)
          .then(() => waitForReportToFinish(reportId, token, attempt + 1));
      });

  it("Verify the report is generated successfully and its items can be retrieved", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "check-eligibility/report",
          validPostEligibilityReportRequest(),
          token,
        ).then((response) => {
          cy.verifyPostEligibilityReportResponse(response);
          const reportId: string = response.body.data.reportID;

          waitForReportToFinish(reportId, token).then((finalStatus) => {
            expect(finalStatus).to.eq("Complete");

            cy.apiRequest(
              "GET",
              `check-eligibility/report/${reportId}/items`,
              null,
              token,
            ).then((itemsResponse) => {
              cy.verifyApiResponseCode(itemsResponse, 200);
            });
          });
        });
      },
    );
  });
});

// If request has bad data, such invalid dates, local auth code etc
describe("Post Eligibility Report - Invalid Requests", () => {
  const invalidEligibilityReportRequest = invalidPostEligibilityReportRequest();
  it("Verify 400 Bad Request response is returned with invalid data", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyTelford).then(
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
