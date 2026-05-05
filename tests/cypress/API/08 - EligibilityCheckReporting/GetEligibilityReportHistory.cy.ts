import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

const reportGeneratedWithinLast7Days = () => {
  const today = new Date();

  // pick a random number between 0 and 6
  const daysAgo = Math.floor(Math.random() * 7);

  const dt = new Date(today);
  dt.setDate(today.getDate() - daysAgo);

  return dt.toISOString();
};

describe("Get Eligibility Report History - Valid Requests", () => {
  it("stubs the GET request correctly", () => {
    cy.intercept("GET", "**/check-eligibility/report-history/123", {
      statusCode: 200,
      body: {
        data: [
          {
            reportGeneratedDate: reportGeneratedWithinLast7Days(),
            startDate: "2026-03-01T00:00:00Z",
            endDate: "2026-03-10T00:00:00Z",
            generatedBy: "Steve Smith",
            numberOfResults: 18,
          },
          {
            reportGeneratedDate: reportGeneratedWithinLast7Days(),
            startDate: "2026-03-05T00:00:00Z",
            endDate: "2026-03-12T00:00:00Z",
            generatedBy: "John Doe",
            numberOfResults: 9,
          },
        ],
      },
    }).as("stubEligibilityHistory");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.window().then((win) => {
          return win
            .fetch(`check-eligibility/report-history/123`, {
              method: "GET",
              headers: {
                Authorization: `Bearer ${token}`,
                "Content-Type": "application/json",
              },
            })
            .then(async (fetchResponse) => {
              const json = await fetchResponse.json();

              const wrappedResponse = {
                status: fetchResponse.status,
                body: json,
              };

              cy.verifyEligibilityReportHistoryResponse(wrappedResponse);
            });
        });
      },
    );

    cy.wait("@stubEligibilityHistory");
  });
});
