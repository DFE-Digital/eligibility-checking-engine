// /FreeSchoolMeals/{guid
import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody, validHMRCRequestBody, notEligibleHomeOfficeRequestBody } from '../../support/requestBodies';

const validRequestBody = validHMRCRequestBody();
const notEligibleRequestBody = notEligibleHomeOfficeRequestBody();

describe('GET eligibility soft check  Status ', () => {
  it('Verify 200 Success response is returned with valid guid', () => {
    cy.createEligibilityCheckAndGetStatus('/oauth2/token', validLoginRequestBody, 'check/free-school-meals', validRequestBody);
  });

  it('Verify 404 Not Found response is returned with invalid guid', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('GET', 'check/7fc12dc9-5a9d-4155-887c-d2b3d60384e/Status', {}, token).then((response) => {
        cy.verifyApiResponseCode(response, 404);
        expect(response.body).to.have.property("errors");
      });
    });
  });
});

describe('GET eligibility soft check Status with type', () => {
  it('Verify 200 Success response is returned with valid guid', () => {
    cy.createEligibilityCheckAndGetStatus('/oauth2/token', validLoginRequestBody, 'check/free-school-meals', validRequestBody);
  });

  it('Verify 404 Not Found response is returned with invalid guid', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('GET', 'check/FreeSchoolMeals/7fc12dc9-5a9d-4155-887c-d2b3d60384e/Status', {}, token).then((response) => {
        cy.verifyApiResponseCode(response, 404);
        expect(response.body).to.have.property("errors");
      });
    });
  });
});


describe('Verify Eligibility Check Statuses', () => {


  it('Verify Eligible status is returned', () => {
    var eligibleBody = validHMRCRequestBody();
    eligibleBody.data.lastName = "Tester";
    cy.createEligibilityCheckAndGetStatus('/oauth2/token', validLoginRequestBody, 'check/free-school-meals', eligibleBody)
    cy.get('@status').then((status: any) => {
      expect(status).to.equal('eligible')
    })
  })

  it('Verify parentNotFound status is returned', () => {
    cy.createEligibilityCheckAndGetStatus('/oauth2/token', validLoginRequestBody, 'check/free-school-meals', notEligibleRequestBody)
    cy.get('@status').then((status: any) => {
      expect(status).to.equal('parentNotFound')
    })
  })

  xit('Verify queuedForProcessing status is returned', () => {
    cy.updateLastName(notEligibleRequestBody).then((updatedRequestBody) => {
      cy.createEligibilityCheckAndGetStatus('/oauth2/token', validLoginRequestBody, 'check/free-school-meals', updatedRequestBody)
      cy.get('@status').then((status: any) => {
        expect(status).to.equal('queuedForProcessing')
      })

    })
  })
})