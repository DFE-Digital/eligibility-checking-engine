import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody, validWorkingFamiliesBulkRequestBody } from '../../support/requestBodies';

const validRequestBody = validWorkingFamiliesBulkRequestBody();

describe('GET eligibility bulk check Results ', () => {
  it('Verify 200 Success response is returned with valid guid', () => {
    cy.createEligibilityBulkCheckAndGetResults('/oauth2/token', validLoginRequestBody, 'bulk-check/working-families', validRequestBody);
  });

  it('Verify progress response contains all expected properties', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', validRequestBody, token).then((createResponse) => {
        cy.verifyApiResponseCode(createResponse, 202);

        const progressUrl = createResponse.body.links.get_Progress_Check;
        const resultsUrl = createResponse.body.links.get_BulkCheck_Results;

        cy.waitForBulkCompletion(progressUrl, token).then(() => {
          cy.apiRequest('GET', progressUrl, {}, token).then((response) => {
            cy.verifyApiResponseCode(response, 200);
            expect(response.body).to.have.all.keys('data', 'links');
            expect(response.body.data).to.have.all.keys('status', 'total', 'complete');
            expect(response.body.links).to.have.all.keys('get_BulkCheck_Results');
            expect(response.body.links.get_BulkCheck_Results).to.equal(resultsUrl);
            expect(resultsUrl).to.equal(progressUrl.replace(/\/progress$/, '/'));
          });
        });
      });
    });
  });

  it('Verify 404 Not Found response is returned with invalid guid', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('GET', 'bulk-check/7fc12dc9-5a9d-4155-887c-d2b3d60384e/progress', {}, token).then((response) => {
        cy.verifyApiResponseCode(response, 404)
      });
    });
  });

});

describe('Verify Eligibility Check Statuses', () => {

  it('Verify Eligible status is returned', () => {
    cy.createEligibilityBulkCheckAndGetResults('/oauth2/token', validLoginRequestBody, 'bulk-check/working-families', validRequestBody);
    cy.get('@data').then((data: any) => {
      data.forEach((check) => {
        if (check.eligibilityCode.startsWith("900")) {
          expect(check.status).to.equal("eligible");
        // } else if (check.eligibilityCode.startWith('') {
        //   expect(check.status).to.equal("notEligible");
        // } else {
        //   expect(check.status).to.equal("notFound");
        // }
      }
      })
    })
  })
});