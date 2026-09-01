import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validFosterFamilyRequestBody,
  validLoginRequestBodyFosterFamilies,
} from "@/cypress/support/requestBodies";

describe("Search Foster Families - Happy Path", () => {
  it("GET - Should return matching foster families", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = validFosterFamilyRequestBody();

      cy.apiRequest("POST", "/foster-family", request, token).then(() => {
        cy.wait(3000);

        cy.request({
          method: "GET",
          url: `${Cypress.config("baseUrl")}/foster-family/search?pageNumber=1&pageSize=10`,
          headers: {
            Authorization: `Bearer ${token}`,
          },
        }).then((response) => {
          expect(response.status).to.eq(200);

          expect(response.body.data).to.be.an("array");

          const family = response.body.data.find(
            (x: any) =>
              x.carerName ===
              `${request.fosterCarer.carerFirstName} ${request.fosterCarer.carerLastName}`,
          );

          expect(family).to.exist;

          // clean up
          cy.apiRequest(
            "DELETE",
            `/foster-family/${family.carerId}`,
            null,
            token,
          ).then((deleteResponse) => {
            expect(deleteResponse.status).to.eq(204);

            // verify fam is gone.
            cy.apiRequest(
              "GET",
              `/foster-family/${family.carerId}`,
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

describe("Search Foster Families - Unhappy Paths", () => {
  it("GET - Should return 400 when page number is less than 1", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: `${Cypress.config("baseUrl")}/foster-family/search?pageNumber=-1&pageSize=10`,
        headers: {
          Authorization: `Bearer ${token}`,
        },
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(400);

        expect(response.body.errors).to.have.length(1);
        expect(response.body.errors[0].title).to.eq("Invalid page number");
      });
    });
  });

  it("GET - Should return 400 when page number is zero", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: `${Cypress.config("baseUrl")}/foster-family/search?pageNumber=0&pageSize=10`,
        headers: {
          Authorization: `Bearer ${token}`,
        },
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(400);

        expect(response.body.errors[0].title).to.eq("Invalid page number");
      });
    });
  });

  it("GET - Should return 400 when page size is zero", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: `${Cypress.config("baseUrl")}/foster-family/search?pageNumber=1&pageSize=0`,
        headers: {
          Authorization: `Bearer ${token}`,
        },
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(400);

        expect(response.body.errors[0].title).to.eq("Invalid page size");
      });
    });
  });

  it("GET - Should return 400 when page size is greater than 10", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: `${Cypress.config("baseUrl")}/foster-family/search?pageNumber=1&pageSize=11`,
        headers: {
          Authorization: `Bearer ${token}`,
        },
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(400);

        expect(response.body.errors[0].title).to.eq("Invalid page size");
      });
    });
  });
});
