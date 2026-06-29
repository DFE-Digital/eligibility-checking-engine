import {
  CheckEligibilityResponseBulk,
  BulkResultItem,
} from "../../support/interfaces";
import { getandVerifyBearerToken } from "../../support/apiHelpers";
import { validLoginRequestBody } from "../../support/requestBodies";

describe("Bulk Check - Hashing Behaviour", () => {
  const request = {
    data: [
      {
        nationalInsuranceNumber: "NN123456C",
        lastName: "Tester",
        dateOfBirth: "2001-01-01",
        nationalAsylumSeekerServiceNumber: "",
      },
      {
        nationalInsuranceNumber: "NN123456C",
        lastName: "Tester",
        dateOfBirth: "2001-01-01",
        nationalAsylumSeekerServiceNumber: "",
      },
    ],
  };

  it("Batch submitted with all new checks → all processed", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "POST",
          `bulk-check/free-school-meals`,
          request,
          token,
        ).then((response: { body: CheckEligibilityResponseBulk }) => {
          //  response validation
          cy.verifyApiResponseCode(response, 202);
          cy.verifyPostEligibilityBulkCheckResponse(response);

          const links = response.body.links;

          const progress = links.get_Progress_Check;

          // wait for async completion
          cy.waitForBulkCompletion(progress, token).then(() => {
            // fetch results
            cy.apiRequest("GET", links.get_BulkCheck_Results, {}, token).then(
              (response: { body: { data: BulkResultItem[] } }) => {
                cy.verifyBulkResults(response.body.data, request.data);
              },
            );
          });
        });
      },
    );
  });

  it("Batch submitted with all hashed checks → reused results", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest("POST", `bulk-check/free-school-meals`, request, token)
          .then((response) => {
            const links = response.body.links;

            return cy.waitForBulkCompletion(links.get_Progress_Check, token);
          })

          // should now be hashed
          .then(() => {
            return cy.apiRequest(
              "POST",
              `bulk-check/free-school-meals`,
              request,
              token,
            );
          })
          .then((response: { body: CheckEligibilityResponseBulk }) => {
            const links = response.body.links;

            return cy
              .waitForBulkCompletion(links.get_Progress_Check, token)
              .then(() =>
                cy.apiRequest("GET", links.get_BulkCheck_Results, null, token),
              );
          })
          .then((response: { body: { data: BulkResultItem[] } }) => {
            expect(response.body.data.length).to.equal(2);
          });
      },
    );
  });

  it("Batch submitted with mix of hashed and new checks", () => {
    const mixedRequest = {
      data: [
        request.data[0], // hashed record
        {
          nationalInsuranceNumber: "NN123456C",
          lastName: "NewUser",
          dateOfBirth: "2005-05-05",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        // first run
        cy.apiRequest("POST", `bulk-check/free-school-meals`, request, token)
          .then((res) => {
            const links = res.body.links;
            return cy.waitForBulkCompletion(links.get_Progress_Check, token);
          })

          // second run (mixed)
          .then(() => {
            return cy.apiRequest(
              "POST",
              `bulk-check/free-school-meals`,
              mixedRequest,
              token,
            );
          })
          .then((res) => {
            const links = res.body.links;

            return cy
              .waitForBulkCompletion(links.get_Progress_Check, token)
              .then(() =>
                cy.apiRequest("GET", links.get_BulkCheck_Results, null, token),
              );
          })
          .then((response) => {
            expect(response.body.data.length).to.equal(2);
          });
      },
    );
  });

  //  Multiple batches

  it("Multiple small batches submitted concurrently", () => {
    const small1 = {
      data: [
        {
          clientIdentifier: "123",
          nationalInsuranceNumber: "NN123456C",
          lastName: "Tester",
          dateOfBirth: "2001-01-01",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    const small2 = {
      data: [
        {
          clientIdentifier: "123",
          nationalInsuranceNumber: "NN123456C",
          lastName: "Tester",
          dateOfBirth: "2001-01-01",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "POST",
          "bulk-check/free-school-meals",
          small1,
          token,
        ).then((res1) => {
          cy.verifyApiResponseCode(res1, 202);
          cy.verifyPostEligibilityBulkCheckResponse(res1);

          const links1 = res1.body.links;

          // fire second batch
          cy.apiRequest(
            "POST",
            "bulk-check/free-school-meals",
            small2,
            token,
          ).then((res2) => {
            cy.verifyApiResponseCode(res2, 202);
            cy.verifyPostEligibilityBulkCheckResponse(res2);

            const links2 = res2.body.links;

            //  wait for both batches to complete
            cy.waitForBulkCompletion(links1.get_Progress_Check, token).then(
              () => {
                cy.waitForBulkCompletion(links2.get_Progress_Check, token).then(
                  () => {
                    // fetch results for both
                    cy.apiRequest(
                      "GET",
                      links1.get_BulkCheck_Results,
                      {},
                      token,
                    ).then((r1) => {
                      cy.verifyApiResponseCode(r1, 200);
                      expect(r1.body.data.length).to.equal(1);
                    });

                    cy.apiRequest(
                      "GET",
                      links2.get_BulkCheck_Results,
                      {},
                      token,
                    ).then((r2) => {
                      cy.verifyApiResponseCode(r2, 200);
                      expect(r2.body.data.length).to.equal(1);
                    });
                  },
                );
              },
            );
          });
        });
      },
    );
  });

  it("Multiple small batches submitted concurrently with mix of hashed and new checks", () => {
    const smallBase = {
      data: [
        {
          nationalInsuranceNumber: "NN123456C",
          lastName: "Small",
          dateOfBirth: "2001-01-01",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    const mixedSmall = {
      data: [
        {
          nationalInsuranceNumber: "NN123456C", // hashed
          lastName: "Small",
          dateOfBirth: "2001-01-01",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    const newSmall = {
      data: [
        {
          nationalInsuranceNumber: "NN123456C", //  new
          lastName: "SmallNew",
          dateOfBirth: "2005-05-05",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        // create hash
        cy.apiRequest("POST", "bulk-check/free-school-meals", smallBase, token)
          .then((res) => {
            return cy.waitForBulkCompletion(
              res.body.links.get_Progress_Check,
              token,
            );
          })

          // Concurrent second runs (mixed)
          .then(() => {
            cy.apiRequest(
              "POST",
              "bulk-check/free-school-meals",
              mixedSmall,
              token,
            ).then((res1) => {
              const links1 = res1.body.links;

              cy.apiRequest(
                "POST",
                "bulk-check/free-school-meals",
                newSmall,
                token,
              ).then((res2) => {
                const links2 = res2.body.links;

                cy.waitForBulkCompletion(links1.get_Progress_Check, token).then(
                  () => {
                    cy.waitForBulkCompletion(
                      links2.get_Progress_Check,
                      token,
                    ).then(() => {
                      cy.apiRequest(
                        "GET",
                        links1.get_BulkCheck_Results,
                        null,
                        token,
                      ).then((r1) => {
                        expect(r1.body.data.length).to.equal(1);
                      });

                      cy.apiRequest(
                        "GET",
                        links2.get_BulkCheck_Results,
                        null,
                        token,
                      ).then((r2) => {
                        expect(r2.body.data.length).to.equal(1);
                      });
                    });
                  },
                );
              });
            });
          });
      },
    );
  });

  it("Multiple small batches submitted concurrently with all hashed checks", () => {
    const small = {
      data: [
        {
          nationalInsuranceNumber: "NN123456C",
          lastName: "Small",
          dateOfBirth: "2001-01-01",
          nationalAsylumSeekerServiceNumber: "",
        },
      ],
    };

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        // create hash
        cy.apiRequest("POST", "bulk-check/free-school-meals", small, token)
          .then((res) => {
            return cy.waitForBulkCompletion(
              res.body.links.get_Progress_Check,
              token,
            );
          })

          // Concurrent hashed runs
          .then(() => {
            cy.apiRequest(
              "POST",
              "bulk-check/free-school-meals",
              small,
              token,
            ).then((res1) => {
              const links1 = res1.body.links;

              cy.apiRequest(
                "POST",
                "bulk-check/free-school-meals",
                small,
                token,
              ).then((res2) => {
                const links2 = res2.body.links;

                // wait for both
                cy.waitForBulkCompletion(links1.get_Progress_Check, token).then(
                  () => {
                    cy.waitForBulkCompletion(
                      links2.get_Progress_Check,
                      token,
                    ).then(() => {
                      cy.apiRequest(
                        "GET",
                        links1.get_BulkCheck_Results,
                        null,
                        token,
                      ).then((r1) => {
                        expect(r1.body.data.length).to.equal(1);
                      });

                      cy.apiRequest(
                        "GET",
                        links2.get_BulkCheck_Results,
                        null,
                        token,
                      ).then((r2) => {
                        expect(r2.body.data.length).to.equal(1);
                      });
                    });
                  },
                );
              });
            });
          });
      },
    );
  });
});
