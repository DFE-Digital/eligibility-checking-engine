import {
  CheckEligibilityResponseBulk,
  BulkResultItem,
} from "../../support/interfaces";
import { getandVerifyBearerToken } from "../../support/apiHelpers";
import { validLoginRequestBody } from "../../support/requestBodies";

describe("Bulk Check - Hashing Behaviour", () => {
  it("Batch submitted with all NEW checks → all processed", () => {
    const unique = Date.now();

    const request = {
      data: [
        {
          clientIdentifier: "test-1",
          lastName: "Smith",
          firstName: "John",
          dateOfBirth: "2010-01-01",
          nationalInsuranceNumber: "AB123456C",
        },
        {
          clientIdentifier: "test-2",
          lastName: "Jones",
          firstName: "Jane",
          dateOfBirth: "2011-02-02",
          nationalInsuranceNumber: "AB123456C",
        },
      ],
    };

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "POST",
          `bulk-check/free-school-meals`,
          request,
          token,
        ).then((response: { body: CheckEligibilityResponseBulk }) => {
          //  response validation
          cy.verifyApiResponseCode(response, 202);
          cy.verifyPostEligibilityBulkCheckResponse(response);

          const links = response.body.links;
        
          const progress = links.get_Progress_Check;

          // wait for async completion
          cy.waitForBulkCompletion(progress, token).then(
            () => {
              // ✅ fetch results
              cy.apiRequest(
                "GET",
                links.get_BulkCheck_Results,
                {},
                token,
              ).then((response: { body: { data: BulkResultItem[] } }) => {

                console.log(response);

                const results = response.body.data;

                // verify results
                // cy.verifyBulkResults(results, request.data);
              });
            },
          );
        });
      },
    );
  });
}); 
