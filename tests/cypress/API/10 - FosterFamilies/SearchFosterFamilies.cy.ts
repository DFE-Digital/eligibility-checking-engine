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

      cy.apiRequest("POST", "/foster-family", request, token).then((createResponse) => {
        cy.apiRequest("GET", `/foster-family/search?pageNumber=1&pageSize=10&ninoFilter=${encodeURIComponent(request.fosterCarer.carerNationalInsuranceNumber)}`, null, token).then((response) => {
          expect(response.status).to.eq(200);
          expect(response.body.data).to.be.an("array");
          expect(response.body.totalNumberOfRecords).to.eq(1);

          const family = response.body.data.find(
            (x: any) =>
              x.carerName ===
              `${request.fosterCarer.carerFirstName} ${request.fosterCarer.carerLastName}`,
          );

          expect(family).to.exist;
          expect(family.fosterCarerId).to.eq(createResponse.body.fosterCarerId);

          // clean up
          cy.apiRequest("DELETE", `/foster-family/${family.fosterCarerId}`, null, token, false).then((deleteResponse) => {
            expect(deleteResponse.status).to.eq(204);

            // verify fam is gone.
            cy.apiRequest("GET", `/foster-family/${family.fosterCarerId}`, null, token, false,).then((getResponse) => {
              expect(getResponse.status).to.eq(404);
            });
          });
        });
      });
    });
  });

  it("GET - Should filter on partner NINO and return paginated child records", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      const request = validFosterFamilyRequestBody();

      cy.apiRequest("POST", "/foster-family", request, token).then(
        (createFamilyResponse) => {
          const fosterCarerId = createFamilyResponse.body.fosterCarerId;
          const initialChildId = createFamilyResponse.body.fosterChildId;

          cy.apiRequest(
            "POST",
            `/foster-family/${fosterCarerId}/child`,
            {
              childFirstName: "Page",
              childLastName: "Two",
              childDateOfBirth: "2023-02-02",
              childPostCode: "AB1 2CD",
            },
            token,
          ).then((createChildResponse) => {
            const addedChildId = createChildResponse.body.fosterChildId;
            const query = `ninoFilter=${encodeURIComponent(request.partner.partnerNationalInsuranceNumber)}`;

            cy.apiRequest(
              "GET",
              `/foster-family/search?pageNumber=1&pageSize=1&${query}`,
              null,
              token,
            ).then((firstPageResponse) => {
              expect(firstPageResponse.status).to.eq(200);
              expect(firstPageResponse.body.totalNumberOfRecords).to.eq(2);
              expect(firstPageResponse.body.pageNumber).to.eq(1);
              expect(firstPageResponse.body.pageSize).to.eq(1);
              expect(firstPageResponse.body.data).to.have.length(1);

              cy.apiRequest(
                "GET",
                `/foster-family/search?pageNumber=2&pageSize=1&${query}`,
                null,
                token,
              ).then((secondPageResponse) => {
                expect(secondPageResponse.status).to.eq(200);
                expect(secondPageResponse.body.totalNumberOfRecords).to.eq(2);
                expect(secondPageResponse.body.pageNumber).to.eq(2);
                expect(secondPageResponse.body.data).to.have.length(1);

                const returnedChildIds = [
                  firstPageResponse.body.data[0].fosterChildId,
                  secondPageResponse.body.data[0].fosterChildId,
                ];
                expect(returnedChildIds).to.have.members([
                  initialChildId,
                  addedChildId,
                ]);

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
        },
      );
    });
  });
});

describe("Search Foster Families - Unhappy Paths", () => {
  it("GET - Should return 400 when the NINO filter is invalid", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: "/foster-family/search?pageNumber=1&pageSize=10&ninoFilter=INVALID",
        headers: {
          Authorization: `Bearer ${token}`,
        },
        failOnStatusCode: false,
      }).then((response) => {
        expect(response.status).to.eq(400);
      });
    });
  });

  it("GET - Should return 400 when page number is less than 1", () => {
    getandVerifyBearerToken(
      "/oauth2/token",
      validLoginRequestBodyFosterFamilies,
    ).then((token) => {
      cy.request({
        method: "GET",
        url: `/foster-family/search?pageNumber=-1&pageSize=10`,
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
        url: `/foster-family/search?pageNumber=0&pageSize=10`,
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
        url: `/foster-family/search?pageNumber=1&pageSize=0`,
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
        url: `/foster-family/search?pageNumber=1&pageSize=11`,
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
