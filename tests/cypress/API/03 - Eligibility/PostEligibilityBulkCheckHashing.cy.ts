import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

describe("Bulk Check - Hashing Behaviour", () => {
  it("Batch submitted with all NEW checks → all processed", () => {
    const unique = Date.now();

    const request = {
      data: [
        {
          clientIdentifier: `test-${unique}-1`,
          lastName: `Smith${unique}`,
          firstName: "John",
          dateOfBirth: "2010-01-01",
          nationalInsuranceNumber: `AB12${unique.toString().slice(-4)}C`,
        },
        {
          clientIdentifier: `test-${unique}-2`,
          lastName: `Jones${unique}`,
          firstName: "Jane",
          dateOfBirth: "2011-02-02",
          nationalInsuranceNumber: `CD34${unique.toString().slice(-4)}E`,
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
        ).then((response) => {

          cy.verifyApiResponseCode(response, 202);
          cy.verifyPostEligibilityBulkCheckResponse(response);
            
          ///cy.
          /// more to do, check status etc... 

        });
      },
    );

 
  });
});

