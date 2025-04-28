// /FreeSchoolMeals/{guid
import { getandVerifyBearerToken } from '../../support/apiHelpers';
import {
    validLoginRequestBody,
    validApplicationRequestBody,
    validApplicationSupportRequestBody,
    validUserRequestBody
} from '../../support/requestBodies';




describe('GET eligibility soft check by Guid', () => {
    const validApplicationRequest = validApplicationRequestBody();

    it('Verify 200 Success response is returned with valid guid', () => {
        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            const requestBody = validApplicationSupportRequestBody();
            cy.apiRequest('POST', 'check/free-school-meals', requestBody, token).then((response) => {
                cy.verifyApiResponseCode(response, 202);
                cy.apiRequest('POST', '/user', validUserRequestBody(), token).then((response) => {
                    validApplicationRequest.Data.UserId = response.Data;
                    cy.wait(60000);

                    //Make post request for eligibility check
                    cy.apiRequest('POST', 'application', validApplicationRequest, token).then((response) => {
                        cy.verifyApiResponseCode(response, 201);
                        //extract Guid
                        cy.extractGuid(response);

                        //make get request using the guid 
                        cy.get('@Guid').then((Guid) => {
                            cy.apiRequest('GET', `application/${Guid}`, {}, token).then((newResponse) => {
                                // Assert the response 
                                cy.verifyApiResponseCode(newResponse, 200)
                                cy.log(JSON.stringify(validApplicationRequest))
                                cy.log(JSON.stringify(newResponse))
                                cy.verifyGetApplicationResponse(newResponse, validApplicationRequest)
                            })
                        });
                    });
                });
            });
        });
    })
    it('Verify 401 response is returned when bearer token is not provided', () => {
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            //Make post request for eligibility check
            cy.apiRequest('POST', 'application', validApplicationRequest, token).then((response) => {
                cy.verifyApiResponseCode(response, 201);
                //extract Guid
                cy.extractGuid(response);
                //make get request using the guid 
                cy.get('@Guid').then((Guid) => {
                    cy.apiRequest('GET', `application/${Guid}`, {} ).then((newResponse) => {
                        // Assert the response 
                        cy.verifyApiResponseCode(newResponse, 401)
                    })
                });
            });
        });
    })
})