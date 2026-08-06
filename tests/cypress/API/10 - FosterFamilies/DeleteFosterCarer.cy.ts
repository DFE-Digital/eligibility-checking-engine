import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Delete Foster Carer - happy paths", () => {
  it("DELETE - Should delete the foster carer", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        // create fam
        cy.apiRequest(
          "POST",
          "/foster-family",
          validFosterFamilyRequestBody(),
          token,
        ).then((createResponse) => {
          const fosterCarerId = createResponse.body.fosterCarerId;

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

describe("Delete Foster Partner - happy paths", () => {
  it("DELETE - Should return 204 when foster partner is deleted", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "/foster-family",
          validFosterFamilyRequestBody(),
          token,
        ).then((createResponse) => {
          const fosterCarerId = createResponse.body.fosterCarerId;

          cy.apiRequest(
            "DELETE",
            `/foster-family/${fosterCarerId}/partner`,
            null,
            token,
          ).then((response) => {
            expect(response.status).to.eq(204);
          });
        });
      },
    );
  });
});

describe("Delete Foster Carer - unhappy paths", () => {
  it("DELETE - Should return 404 when foster carer does not exist", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "DELETE",
          `/foster-family/${crypto.randomUUID()}`,
          null,
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(404);
        });
      },
    );
  });

  it("DELETE - Should return 400 when foster carer id is invalid", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "DELETE",
          "/foster-family/not-a-guid",
          null,
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(400);
        });
      },
    );
  });

  it("DELETE - Should return 401 when bearer token is missing", () => {
    cy.apiRequest(
      "DELETE",
      `/foster-family/${crypto.randomUUID()}`,
      null,
      null,
      false,
    ).then((response) => {
      expect(response.status).to.eq(401);
    });
  });
});
