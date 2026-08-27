import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  updateFosterChildRequestBody,
  invalidUpdateFosterChildRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Update Foster Child - happy paths", () => {
  it("PATCH - Should update a foster child", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        // Create fam
        cy.apiRequest(
          "POST",
          "/foster-family",
          validFosterFamilyRequestBody(),
          token,
        ).then((createFamilyResponse) => {
          const fosterCarerId = createFamilyResponse.body.fosterCarerId;

          // Get fam
          cy.apiRequest(
            "GET",
            `/foster-family/${fosterCarerId}?includeChildren=true`,
            null,
            token,
          ).then((familyResponse) => {
            const fosterChildId =
              familyResponse.body.fosterChildren[0].fosterChildId;
            const updateRequest = updateFosterChildRequestBody();

            // Update child
            cy.apiRequest(
              "PATCH",
              `/foster-family/child/${fosterChildId}`,
              updateRequest,
              token,
            ).then((response) => {
              console.log(familyResponse.body);

              expect(response.status).to.eq(200);

              expect(response.body.fosterChildId).to.eq(fosterChildId);

              expect(response.body.childFullName).to.eq(
                "Updated Tom Updated Smith",
              );

              expect(response.body.postCode).to.eq("AB1 2CD");

              // clean up
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
          });
        });
      },
    );
  });
});

describe("Update Foster Child - unhappy paths", () => {
  it("PATCH - Should return 404 when foster child does not exist", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "PATCH",
          `/foster-family/child/${crypto.randomUUID()}`,
          updateFosterChildRequestBody(),
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(404);
        });
      },
    );
  });

  it("PATCH - Should return 400 when request is invalid", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "PATCH",
          `/foster-family/child/${crypto.randomUUID()}`,
          invalidUpdateFosterChildRequestBody(),
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(400);
        });
      },
    );
  });
});
