// /FreeSchoolMeals/{guid
import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody, validHMRCRequestBody, validWorkingFamiliesRequestBody } from '../../support/requestBodies';


describe('GET eligibility soft check by Guid', () => {
    it('Verify 200 Success response is returned with valid guid', () => {
        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            //Make post request for eligibility check
            cy.log(Cypress.env('lastName'));
            const requestBody = validHMRCRequestBody();
            cy.apiRequest('POST', 'check/free-school-meals', requestBody, token).then((response) => {
                cy.verifyApiResponseCode(response, 202);
                //extract Guid
                cy.extractGuid(response);

                //make get request using the guid 
                cy.get('@Guid').then((Guid) => {
                    cy.apiRequest('GET', `check/${Guid}`, {}, token).then((newResponse) => {
                        // Assert the response 
                        cy.verifyApiResponseCode(newResponse, 200)
                        // requestBody.data.lastName = requestBody.data.lastName.toUpperCase();
                        cy.verifyGetEligibilityCheckResponseData(newResponse, requestBody)
                    })
                });
            });
        });
    })
    it('Verify 200 Success response is returned with valid guid Working Families', () => {
        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            //Make post request for eligibility check
            cy.log(Cypress.env('lastName'));
            const requestBody = validWorkingFamiliesRequestBody();
            cy.apiRequest('POST', 'check/working-families', requestBody, token).then((response) => {
                cy.verifyApiResponseCode(response, 202);
                //extract Guid
                cy.extractGuid(response);
                //make get request using the guid 
                cy.get('@Guid').then((Guid) => {
                    cy.apiRequest('GET', `check/${Guid}`, {}, token).then((newResponse) => {
                        // Assert the response 
                        cy.verifyApiResponseCode(newResponse, 200)
                        cy.verifyGetEligibilityWFCheckResponseData(newResponse, requestBody)
                    })
                });
            });
        });
    })
    it('Verify 404 Not Found response is returned with invalid guid', () => {
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('GET', 'free-school-meals/7fc12dc9-5a9d-4155-887c-d2b3d60384e', {}, token).then((response) => {
                cy.verifyApiResponseCode(response, 404)
            });
        });
    });

})