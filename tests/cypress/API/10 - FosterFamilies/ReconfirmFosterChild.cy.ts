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

              cy.apiRequest("POST", `/foster-family/child/${child.fosterChildId}/reconfirm`, validFosterChildReconfirmBody(), token)
                .then((reconfirmResponse) => {
                  expect(reconfirmResponse.status).to.eq(200);
                  expect(reconfirmResponse.body.fosterChildId).to.eq(child.fosterChildId);
                  expect(reconfirmResponse.body.eligibilityCode).to.eq(child.eligibilityCode);
                  expect(reconfirmResponse.body.carerName).to.exist;

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

describe("Preview Foster Child Reconfirmation", () => {
  it("POST - Should return preview dates without changing the foster child", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        "/foster-family",
        validFosterFamilyRequestBody(),
        token,
      ).then((createFamilyResponse) => {
        const fosterCarerId = createFamilyResponse.body.fosterCarerId;
        const fosterChildId = createFamilyResponse.body.fosterChildId;

        cy.apiRequest(
          "GET",
          `/foster-family/child/${fosterChildId}`,
          null,
          token,
        ).then((beforeResponse) => {
          cy.apiRequest(
            "POST",
            `/foster-family/child/${fosterChildId}/preview-reconfirm`,
            validFosterChildReconfirmBody(),
            token,
          ).then((previewResponse) => {
            expect(previewResponse.status).to.eq(200);
            expect(previewResponse.body.validityStartDate).to.exist;
            expect(previewResponse.body.validFromTerm).to.exist;
            expect(previewResponse.body.reconfirmBetweenStart).to.exist;
            expect(previewResponse.body.reconfirmBetweenEnd).to.exist;
            expect(previewResponse.body.gracePeriodEndDate).to.exist;

            cy.apiRequest(
              "GET",
              `/foster-family/child/${fosterChildId}`,
              null,
              token,
            ).then((afterResponse) => {
              expect(afterResponse.status).to.eq(200);
              expect(afterResponse.body.eligibilityCode).to.eq(
                beforeResponse.body.eligibilityCode,
              );

              cy.apiRequest(
                "DELETE",
                `/foster-family/${fosterCarerId}`,
                null,
                token,
              ).then((deleteResponse) => {
                expect(deleteResponse.status).to.eq(204);
              });
            });
          });
        });
      });
    });
  });

  it("POST - Should return 404 when the foster child does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        `/foster-family/child/${crypto.randomUUID()}/preview-reconfirm`,
        validFosterChildReconfirmBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });

  it("POST - Should return 400 when the preview request is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = validFosterChildReconfirmBody();
      request.submissionDate = "";

      cy.apiRequest(
        "POST",
        `/foster-family/child/${crypto.randomUUID()}/preview-reconfirm`,
        request,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });
});

describe("Reconfirm Foster Child - unhappy paths", () => {
  it("POST - Should return 404 when the foster child does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        `/foster-family/child/${crypto.randomUUID()}/reconfirm`,
        validFosterChildReconfirmBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });

  it("POST - Should return 400 when the reconfirmation request is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = validFosterChildReconfirmBody();
      request.submissionDate = "";

      cy.apiRequest(
        "POST",
        `/foster-family/child/${crypto.randomUUID()}/reconfirm`,
        request,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });
});
