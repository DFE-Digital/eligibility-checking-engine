import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("GET & POST Foster Family - Happy path", () => {
  it("Should return 200 and foster family details", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      // Create the family
      const request = validFosterFamilyRequestBody();
      cy.apiRequest("POST", "/foster-family", request, token).then(
        (createResponse) => {
          // Verify the created family
          cy.verifyApiResponseCode(createResponse, 201);

          cy.wait(3000); // wait to save

          const fosterCarerId = createResponse.body.fosterCarerId;

          cy.apiRequest(
            "GET",
            `/foster-family/${fosterCarerId}`,
            null,
            token,
          ).then((response) => {
            console.log(response);
            cy.verifyApiResponseCode(response, 200);
            cy.verifyFosterFamilyCreatedAndReturned(response, request);

            // Clean up
              // delete fam
              cy.apiRequest(
                "DELETE",
                `/foster-family/${fosterCarerId}`,
                null,
                token,
              ).then((deleteResponse) => {
                expect(deleteResponse.status).to.eq(204);

                // verify fam is gone.
                cy.apiRequest(
                  "GET",
                  `/foster-family/${fosterCarerId}`,
                  null,
                  token,
                  false,
                ).then((getResponse) => {
                  expect(getResponse.status).to.eq(404);
                });
              });
          });
        },
      );
    });
  });
});

describe("GET & POST Foster Family - Unhappy path", () => {
  it("GET - Should return 404 when foster carer does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "GET",
        `/foster-family/${crypto.randomUUID()}`,
        null,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });

  it("GET - Should return 400 for invalid foster carer id", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "GET",
        "/foster-family/not-a-guid",
        null,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });

  it("POST - Should return 400 when request is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = validFosterFamilyRequestBody();

      request.fosterCarer.carerFirstName = "";

      cy.apiRequest("POST", "/foster-family", request, token, false).then(
        (response) => {
          expect(response.status).to.eq(400);

          expect(response.body.errors).to.have.length.greaterThan(0);
        },
      );
    });
  });
});
