import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validFosterChildRequestBody,
  invalidFosterChildRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Create Foster Child - happy paths", () => {
  it("POST - Should create a foster child and return it in the foster family", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      // Create family
      cy.apiRequest(
        "POST",
        "/foster-family",
        validFosterFamilyRequestBody(),
        token,
      ).then((createFamilyResponse) => {
        const fosterCarerId = createFamilyResponse.body.fosterCarerId;

        // Create second child
        cy.apiRequest(
          "POST",
          `/foster-family/${fosterCarerId}/child`,
          validFosterChildRequestBody(),
          token,
        ).then((createChildResponse) => {
          expect(createChildResponse.status).to.eq(201);

          expect(createChildResponse.body.childName).to.eq("Sam Jones");

          // Get family including children
          cy.apiRequest(
            "GET",
            `/foster-family/${fosterCarerId}?includeChildren=true`,
            null,
            token,
          ).then((familyResponse) => {
            expect(familyResponse.status).to.eq(200);

            expect(familyResponse.body.fosterChildren).to.be.an("array");

            const child = familyResponse.body.fosterChildren.find(
              (x: any) =>
                x.firstName === validFosterChildRequestBody().childFirstName &&
                x.lastName === validFosterChildRequestBody().childLastName,
            );

            expect(child).to.exist;

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
        });
      });
    });
  });
});

describe("Create Foster Child - unhappy paths", () => {
  it("POST - Should return 404 when foster carer does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        `/foster-family/${crypto.randomUUID()}/child`,
        validFosterChildRequestBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });

  it("POST - Should return 400 when request is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        `/foster-family/${crypto.randomUUID()}/child`,
        invalidFosterChildRequestBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });
});
