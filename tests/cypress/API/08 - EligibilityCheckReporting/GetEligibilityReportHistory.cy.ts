import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

const reportGeneratedWithinLast7Days = () => {
  const today = new Date();
  const daysAgo = Math.floor(Math.random() * 7);
  const dt = new Date(today);
  dt.setDate(today.getDate() - daysAgo);
  return dt.toISOString();
};

describe("Get Eligibility Report History - Valid Requests", () => {
  it("returns paged eligibility report history", () => {

    cy.intercept(
      {
        method: "GET",
        pathname: "/check-eligibility/report-history/123",
        query: { pageNumber: "1" },
      },
      {
        statusCode: 200,
        body: {
          pageNumber: 1,
          pageSize: 10,
          totalNumberOfRecords: 2,
          data: [
            {
              reportGeneratedDate: reportGeneratedWithinLast7Days(),
              startDate: "2026-03-01T00:00:00Z",
              endDate: "2026-03-10T00:00:00Z",
              generatedBy: "Steve Smith",
              numberOfResults: 18,
              status: "Complete",
            },
            {
              reportGeneratedDate: reportGeneratedWithinLast7Days(),
              startDate: "2026-03-05T00:00:00Z",
              endDate: "2026-03-12T00:00:00Z",
              generatedBy: "John Doe",
              numberOfResults: 9,
              status: "Complete",
            },
          ],
        },
      }
    ).as("stubEligibilityHistory");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.window().then((win) => {
          return win
            .fetch(
              "/check-eligibility/report-history/123?pageNumber=1",
              {
                method: "GET",
                headers: {
                  Authorization: `Bearer ${token}`,
                  "Content-Type": "application/json",
                },
              }
            )
            .then(async (fetchResponse) => {

              expect(fetchResponse.status).to.eq(200);

              const text = await fetchResponse.text();
              expect(text).to.not.equal("");

              const json = JSON.parse(text);

              cy.verifyEligibilityReportHistoryResponse({
                status: fetchResponse.status,
                body: json,
              });
            });
        });
      }
    );

    cy.wait("@stubEligibilityHistory");
  });
});


describe("Get Eligibility Report History - Unauthorized", () => {
  it("returns 401 when the user is not authorised", () => {

    cy.intercept(
      {
        method: "GET",
        pathname: "/check-eligibility/report-history/123",
        query: { pageNumber: "1" },
      },
      {
        statusCode: 401,
        body: {
          errors: [
            {
              title: "User is not authorised to view this report history",
            },
          ],
        },
      }
    ).as("unauthorisedHistory");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.window().then((win) => {
          return win.fetch(
            "/check-eligibility/report-history/123?pageNumber=1",
            {
              method: "GET",
              headers: {
                Authorization: `Bearer ${token}`,
                "Content-Type": "application/json",
              },
            }
          )
          .then(async (response) => {

            expect(response.status).to.eq(401);

            const text = await response.text();
            expect(text).to.not.equal("");

            const json = JSON.parse(text);

            expect(json).to.have.property("errors");
            expect(json.errors).to.be.an("array").and.not.be.empty;

            expect(json.errors[0]).to.have.property("title");
            expect(json.errors[0].title).to.contain("not authorised");
          });
        });
      }
    );

    cy.wait("@unauthorisedHistory");
  });
});
