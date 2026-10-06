import { getandVerifyBearerToken } from "../../support/apiHelpers";
import {
  validLoginRequestBody,
  validWorkingFamiliesRequestBodyEligible,
} from "../../support/requestBodies";

describe("Working Families Check - Hashing Behaviour", () => {
  const request = validWorkingFamiliesRequestBodyEligible();
  const persistedFields = [
    "nationalInsuranceNumber",
    "lastName",
    "dateOfBirth",
    "eligibilityCode",
    "status",
    "validityStartDate",
    "validityEndDate",
    "gracePeriodEndDate",
  ];

  it("persists all Working Families check data from the original hashed check", () => {
    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        const createAndGetCheck = () =>
          cy.apiRequest("POST", "check/working-families", request, token)
            .then((postResponse) => {
              cy.verifyApiResponseCode(postResponse, 202);
              const checkUrl = postResponse.body.links.get_EligibilityCheck;
              const checkId = checkUrl.substring(checkUrl.lastIndexOf("/") + 1);
              return cy.pollCheckStatus(checkId, token, 10)
                .then((statusResponse) => {
                  cy.log(`statusResponse: ${statusResponse}`);
                  return cy
                    .apiRequest("GET", `check/${checkId}`, null, token)
                    .then((checkResponse) =>
                      cy
                        .verifyApiResponseCode(checkResponse, 200)
                        .then(() => ({
                          postStatus: postResponse.body.data.status,
                          status: statusResponse.body.data.status,
                          data: checkResponse.body.data,
                        })),
                    );
                });
            });

        createAndGetCheck().then((originalCheck) => {
          cy.log(`Original check status: ${originalCheck.status}`);
          expect(originalCheck.status).to.be.oneOf(["eligible", "notEligible"]);
          createAndGetCheck().then((hashedCheck) => {
            expect(hashedCheck.status).to.equal(originalCheck.status);
            expect(hashedCheck.postStatus).to.equal(originalCheck.status);

            persistedFields.forEach((field) => {
              expect(originalCheck.data).to.have.property(field);
              expect(hashedCheck.data).to.have.property(
                field,
                originalCheck.data[field],
              );
            });
          });
        });
      },
    );
  });
});
