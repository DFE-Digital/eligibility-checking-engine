import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Archive and restore foster child", () => {
  it("PATCH - Should archive an active child and restore it", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "POST",
        "/foster-family",
        validFosterFamilyRequestBody(),
        token,
      ).then((createResponse) => {
        const fosterCarerId = createResponse.body.fosterCarerId;
        const fosterChildId = createResponse.body.fosterChildId;

        cy.apiRequest(
          "PATCH",
          `/foster-family/child/archive/${fosterChildId}`,
          null,
          token,
        ).then((archiveResponse) => {
          expect(archiveResponse.status).to.eq(200);
          expect(archiveResponse.body.status).to.eq("Archived");

          cy.apiRequest(
            "PATCH",
            `/foster-family/child/restore/${fosterChildId}`,
            null,
            token,
          ).then((restoreResponse) => {
            expect(restoreResponse.status).to.eq(200);
            expect(restoreResponse.body.status).to.eq("Active");

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

  it("PATCH - Should return 404 when archiving a child that does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "PATCH",
        `/foster-family/child/archive/${crypto.randomUUID()}`,
        null,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });

  it("PATCH - Should return 404 when restoring a child that does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "PATCH",
        `/foster-family/child/restore/${crypto.randomUUID()}`,
        null,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
      });
    });
  });
});
