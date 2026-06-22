import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

describe("GET Establishments By LA id", () => {
  it("Verify 200 Success returns establishments with valid local authority Id", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `/local-authorities/${201}/establishments`,
          {},
          token,
        ).then((response) => {
          cy.verifyApiResponseCode(response, 200);

          expect(response.body.data).to.be.an("array");

          if (response.body.length > 0) {
            expect(response.body[0]).to.have.property("urn");
            expect(response.body[0]).to.have.property("name");
          }
        });
      },
    );
  });

  it("Verify 404 is returned for invalid local authority Id", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        const invalidLocalAuthorityId = 999999;

        cy.apiRequest(
          "GET",
          `/local-authorities/${invalidLocalAuthorityId}/establishments`,
          {},
          token,
          false,
        ).then((response) => {
          cy.verifyApiResponseCode(response, 404);
        });
      },
    );
  });

});

describe("GET Establishments By MAT id", () => {
  it("Verify 200 Success returns establishments with valid MAT Id", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `/multi-academy-trusts/${17101}/establishments`,
          {},
          token,
        ).then((response) => {
          cy.verifyApiResponseCode(response, 200);

          expect(response.body.data).to.be.an("array");

          if (response.body.length > 0) {
            expect(response.body[0]).to.have.property("urn");
            expect(response.body[0]).to.have.property("name");
          }
        });
      },
    );
  });

  it("Verify 404 is returned for invalid MAT Id", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        const invalidMAT = 999999;

        cy.apiRequest(
          "GET",
          `/multi-academy-trusts/${invalidMAT}/establishments`,
          {},
          token,
          false,
        ).then((response) => {
          cy.verifyApiResponseCode(response, 404);
        });
      },
    );
  });

});
