import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import {
  validLoginRequestBody,
  validFosterFamilyRequestBody,
} from "@/cypress/support/requestBodies";

describe("Search Foster Families - happy paths", () => {
  it("GET - Should return matching foster families", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        const request = validFosterFamilyRequestBody();

        // Create fam
        cy.apiRequest(
          "POST",
          "/foster-family?localAuthorityId=201",
          request,
          token,
        ).then(() => {
          cy.wait(3000);

          // Get fam - pagination
          cy.apiRequest(
            "GET",
            "/foster-family/search?localAuthorityId=201",
            {
              pageNumber: 1,
              pageSize: 10,
            },
            token,
          ).then((response) => {
            expect(response.status).to.eq(200);

            expect(response.body.data).to.be.an("array");

            const family = response.body.data.find(
              (x: any) =>
                x.carerName ===
                `${request.fosterCarer.carerFirstName} ${request.fosterCarer.carerLastName}`,
            );

            expect(family).to.exist;
          });
        });
      },
    );
  });

  it("GET - Should default page number to 1 when page number is invalid", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          "/foster-family/search?localAuthorityId=201&pageNumber=-1",
          null,
          token,
        ).then((response) => {
          expect(response.status).to.eq(200);
          expect(response.body.pageNumber).to.eq(1);
        });
      },
    );
  });
});
