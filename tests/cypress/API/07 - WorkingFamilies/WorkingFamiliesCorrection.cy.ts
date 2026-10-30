import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validSupportPortalLoginRequestBody } from "@/cypress/support/requestBodies";

const getSupportToken = () =>
  getandVerifyBearerToken(
    "/oauth2/token",
    validSupportPortalLoginRequestBody
  );

describe("Working Families correction API", () => {
  it("returns 400 for an invalid eligibility code", () => {
    return getSupportToken().then(token =>
      cy.apiRequest(
        "GET",
        "/working-families/NOT_A_REAL_CODE/correction",
        null,
        token
      ).then(response => {
        expect(response.status).to.eq(400);
        expect(response.body.errors[0].title).to.eq("validation_error");
      })
    );
  });

  it("rejects a foster code on lookup", () => {
    return getSupportToken().then(token =>
      cy.apiRequest(
        "GET",
        "/working-families/40000000004/correction",
        null,
        token
      ).then(response => {
        expect(response.status).to.eq(422);
        expect(response.body.errors[0].title)
          .to.eq("foster_code_not_supported");
      })
    );
  });

  it("rejects a foster code on child update", () => {
    return getSupportToken().then(token =>
      cy.apiRequest(
        "PUT",
        "/working-families/40000000004/correction/child",
        {},
        token,
        false,
        "application/json"
      ).then(response => {
        expect(response.status).to.eq(422);
        expect(response.body.errors[0].title)
          .to.eq("foster_code_not_supported");
      })
    );
  });
});