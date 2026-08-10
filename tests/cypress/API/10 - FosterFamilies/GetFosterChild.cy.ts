import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

let newfosterChildId;

describe("Get Foster Child - happy paths", () => {
  it("GET - Should return foster child details", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        const request = validFosterFamilyRequestBody();

        cy.apiRequest(
          "POST",
          "/foster-family",
          request,
          token,
        ).then((createResponse) => {
          const fosterCarerId = createResponse.body.fosterCarerId;

          cy.apiRequest(
            "GET",
            `/foster-family/${fosterCarerId}?includeChildren=true`,
            null,
            token,
          ).then((familyResponse) => {
            const fosterChildId =
              familyResponse.body.fosterChildren[0].fosterChildId;

            cy.apiRequest(
              "GET",
              `/foster-family/child/${fosterChildId}`,
              null,
              token,
            ).then((response) => {
              expect(response.status).to.eq(200);

              expect(response.body.fosterChildId).to.eq(fosterChildId);

              expect(response.body.childFullName).to.contain(
                request.fosterChild.childFirstName,
              );

              expect(response.body.postCode).to.eq(
                request.fosterChild.childPostCode,
              );

              expect(response.body.eligibilityCode).to.not.be.empty;

              newfosterChildId = familyResponse.body.fosterChildren[0].fosterChildId;
            });
          });
        });
      },
    );
  });

  it("GET - Should return foster child and foster carer details", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `/foster-family/child/${newfosterChildId}?localAuthorityId=201&includeFosterCarer=true`,
          null,
          token,
        ).then((response) => {
          expect(response.status).to.eq(200);

          expect(response.body.fosterCarerId).to.not.be.null;

          expect(response.body.carerName).to.not.be.empty;
        });
      },
    );
  });
});

describe("Get Foster Child - unhappy paths", () => {
  it("GET - Should return 404 when foster child does not exist", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "GET",
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
  it("GET - Should return 400 when foster child id is invalid", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBodyFosterFamilies).then(
      (token) => {
        cy.apiRequest(
          "GET",
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

