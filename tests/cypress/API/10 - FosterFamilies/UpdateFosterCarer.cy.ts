import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  updateFosterCarerRequestBody,
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Update Foster Carer - happy paths", () => {
  it("PATCH - Should return 204 when foster carer is updated - happy path", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {

      console.log(validLoginRequestBodyFosterFamilies);
      // create family
      cy.apiRequest(
        "POST",
        "/foster-family",
        validFosterFamilyRequestBody(),
        token,
      ).then((createResponse) => {
        cy.wait(3000);

        const fosterCarerId = createResponse.body.fosterCarerId;

        // update carer
        cy.apiRequest(
          "PATCH",
          `/foster-family/${fosterCarerId}`,
          updateFosterCarerRequestBody(),
          token,
        ).then((response) => {
          expect(response.status).to.eq(204);
        });
      });
    });
  });
});

describe("Update Foster Carer - Unhappy paths", () => {
  it("PATCH - Should return 404 when foster carer does not exist", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "PATCH",
        `/foster-family/${crypto.randomUUID()}`,
        updateFosterCarerRequestBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(404);
        expect(response.body.errors).to.have.length.greaterThan(0);
      });
    });
  });

  it("PATCH - Should return 400 when request body is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = updateFosterCarerRequestBody();

      request.fosterCarerRequest.carerFirstName = "";
      request.fosterCarerRequest.carerLastName = "";

      cy.apiRequest(
        "PATCH",
        `/foster-family/${crypto.randomUUID()}`,
        request,
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
        expect(response.body.errors).to.have.length.greaterThan(0);
      });
    });
  });

  it("PATCH - Should return 400 when foster carer id is not a valid guid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.apiRequest(
        "PATCH",
        "/foster-family/not-a-guid",
        updateFosterCarerRequestBody(),
        token,
        false,
      ).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });
});
