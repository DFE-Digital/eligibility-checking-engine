import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validFosterChildReconfirmBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Reconfirm Foster Child - happy paths", () => {
  it("POST - Should create a foster child, reconfirm the event and return it the up-to-date record", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      // Create family
      cy.apiRequest("POST", "/foster-family", validFosterFamilyRequestBody(), token)
        .then((createFamilyResponse) => {
          const fosterCarerId = createFamilyResponse.body.fosterCarerId;

          // Get family including children
          cy.apiRequest("GET", `/foster-family/${fosterCarerId}?includeChildren=true`, null, token)
            .then((familyResponse) => {
              expect(familyResponse.status).to.eq(200);
              expect(familyResponse.body.fosterChildren).to.be.an("array");

              const child = familyResponse.body.fosterChildren[0];
              expect(child).to.exist;

              cy.apiRequest("POST", `/foster-child/${child.fosterChildId}/reconfirm`, validFosterChildReconfirmBody(), token)
                .then((reconfirmResponse) => {
                  expect(reconfirmResponse.status).to.eq(200);

                  // delete family
                  cy.apiRequest("DELETE", `/foster-family/${fosterCarerId}`, null, token)
                    .then((deleteResponse) => {
                      expect(deleteResponse.status).to.eq(204);

                      // verify family is gone.
                      cy.apiRequest("GET", `/foster-family/${fosterCarerId}`, null, token, false)
                        .then((getResponse) => { expect(getResponse.status).to.eq(404); });
                    });
                });
            });
        });
    });
  });
});
