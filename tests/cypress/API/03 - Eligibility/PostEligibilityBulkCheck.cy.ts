import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { invalidEligiblityCodeBulkRequestBody, validLoginRequestBody, validWorkingFamiliesBulkRequestBody,
    invalidNinoWorkingFamiliesBulkRequestBody, invalidDobWorkingFamiliesBulkRequestBody, 
    invalidLastNameWorkingFamiliesBulkRequestBody, invalidMultiChecksWorkingFamiliesBulkRequestBody}
    from '../../support/requestBodies';


describe('Post Eligibility Bulk Check - Valid Requests', () => {

  const validWorkingFamiliesBulkRequest = validWorkingFamiliesBulkRequestBody();

  it('Verify 202 Accepted response is returned with valid working families data', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', validWorkingFamiliesBulkRequest, token).then((response) => {
        // Assert the status and statusText
        cy.verifyApiResponseCode(response, 202)
        // Assert the response body data
        cy.verifyPostEligibilityBulkCheckResponse(response)
      });
    });
  });
});

describe('Post Eligibility Bulk Check - Invalid Requests', () => {

  const invalidEligiblityCodeBulkRequest = invalidEligiblityCodeBulkRequestBody();
  const invalidNinoBulkRequest = invalidNinoWorkingFamiliesBulkRequestBody();
  const invalidDobBulkRequest = invalidDobWorkingFamiliesBulkRequestBody();
  const invalidLastNameBulkRequest = invalidLastNameWorkingFamiliesBulkRequestBody();
  const invalidMultiCheckBulkRequest = invalidMultiChecksWorkingFamiliesBulkRequestBody();

  it('Verify 400 Bad Request response is returned with invalid Eligiblity code', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', invalidEligiblityCodeBulkRequest, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)
        expect(response.body.errors[0]).to.have.property('title', 'Eligibility code must be 11 digits long');
      });
    });
  });

  it('Verify 400 Bad Request response is returned with invalid National Insurance number', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', invalidNinoBulkRequest, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)
        expect(response.body.errors[0]).to.have.property('title', 'Invalid National Insurance Number');
      });
    });
  });

  it('Verify 400 Bad Request response is returned with invalid date of birth', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', invalidDobBulkRequest, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)
        expect(response.body.errors[0]).to.have.property('title', 'Date of birth is required:- (yyyy-mm-dd)');
      });
    });
  });

  it('Verify 400 Bad Request response is returned with invalid lastname', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', invalidLastNameBulkRequest, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)
        expect(response.body.errors[0]).to.have.property('title', 'LastName contains an invalid character');
      });
    });
  });
  
  it('Verify 400 Bad Request response is returned with multiple invalid checks', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('POST', 'bulk-check/working-families', invalidMultiCheckBulkRequest, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)
        expect(response.body.errors[0]).to.have.property('title', 'Date of birth is required:- (yyyy-mm-dd)');
        expect(response.body.errors[1]).to.have.property('title', 'Eligibility code must be 11 digits long');
      });
    });
  });
});