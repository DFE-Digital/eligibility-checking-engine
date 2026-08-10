import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Delete Foster Child - happy paths", () => {
  it("DELETE - Should delete the foster child", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "/foster-family",
          validFosterFamilyRequestBody(),
          token,
        ).then((createFamilyResponse) => {
          const fosterCarerId = createFamilyResponse.body.fosterCarerId;

          cy.apiRequest(
            "GET",
            `/foster-family/${fosterCarerId}?includeChildren=true`,
            null,
            token,
          ).then((familyResponse) => {
            const fosterChildId =
              familyResponse.body.fosterChildren[0].fosterChildId;

            cy.apiRequest(
              "DELETE",
              `/foster-family/child/${fosterChildId}`,
              null,
              token,
            ).then((deleteResponse) => {
              expect(deleteResponse.status).to.eq(204);

              cy.apiRequest(
                "GET",
                `/foster-family/child/${fosterChildId}`,
                null,
                token,
                false,
              ).then((getResponse) => {
                expect(getResponse.status).to.eq(404);
              });
            });
          });
        });
      },
    );
  });
});

describe("Delete Foster Child - unhappy paths", () => {
  it("DELETE - Should return 404 when foster child does not exist", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "DELETE",
          `/foster-family/child/${crypto.randomUUID()}`,
          null,
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(404);
        });
      },
    );
  });

  it("DELETE - Should return 400 when foster child id is invalid", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "DELETE",
          "/foster-family/child/not-a-guid",
          null,
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(400);
        });
      },
    );
  });
});
