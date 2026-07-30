import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  updateFosterCarerRequestBody,
  validFosterFamilyRequestBody,
  validLoginRequestBody,
} from "@/cypress/support/requestBodies";

describe("Update Foster Carer - happy paths", () => {
  it("PATCH - Should return 204 when foster carer is updated - happy path", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        // create family
        cy.apiRequest(
          "POST",
          "/foster-family?localAuthorityId=201",
          validFosterFamilyRequestBody(),
          token,
        ).then((createResponse) => {
          cy.wait(3000);

          const fosterCarerId = createResponse.body.fosterCarerId;

          // update carer
          cy.apiRequest(
            "PATCH",
            `/foster-family/${fosterCarerId}?localAuthorityId=201`,
            updateFosterCarerRequestBody(),
            token,
          ).then((response) => {
            expect(response.status).to.eq(204);
          });
        });
      },
    );
  });
});

describe("Update Foster Carer - Unhappy paths", () => {
  it("PATCH - Should return 404 when foster carer does not exist", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "PATCH",
          `/foster-family/${crypto.randomUUID()}?localAuthorityId=201`,
          updateFosterCarerRequestBody(),
          token,
          false,
        ).then((response) => {
          expect(response.status).to.eq(404);
          expect(response.body.errors).to.have.length.greaterThan(0);
        });
      },
    );

    it("PATCH - Should return 400 when request body is invalid", () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
        (token) => {
          const request = updateFosterCarerRequestBody();

          request.fosterCarerRequest.carerFirstName = "";
          request.fosterCarerRequest.carerLastName = "";

          cy.apiRequest(
            "PATCH",
            `/foster-family/${crypto.randomUUID()}?localAuthorityId=201`,
            request,
            token,
            false,
          ).then((response) => {
            expect(response.status).to.eq(400);
            expect(response.body.errors).to.have.length.greaterThan(0);
          });
        },
      );
    });

    it("PATCH - Should return 400 when foster carer id is not a valid guid", () => {
      getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
        (token) => {
          cy.apiRequest(
            "PATCH",
            "/foster-family/not-a-guid?localAuthorityId=201",
            updateFosterCarerRequestBody(),
            token,
            false,
          ).then((response) => {
            expect(response.status).to.eq(400);
          });
        },
      );
    });
  });
});
