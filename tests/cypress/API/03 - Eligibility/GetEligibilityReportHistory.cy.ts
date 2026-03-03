import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";


describe("Get Eligibility Report History - Valid Requests", () => {
  const localAuthorityId = "894";
  it("Verify 200 Accepted response is returned with valid data", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `check-eligibility/report-history/${localAuthorityId}`,
          null,
          token,
        ).then((response) => {
          cy.verifyApiResponseCode(response, 200);
          // Assert the response body data

          // check first element in the response array
          cy.verifyEligibilityReportHistoryResponse(response);
        });
      },
    );
  });
});