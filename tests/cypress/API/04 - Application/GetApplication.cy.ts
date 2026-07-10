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
                cy.log(`Check request: ${JSON.stringify(requestBody)}`);
                cy.apiRequest('POST', '/user', validUserRequestBody(), token).then((userResponse) => {
                    cy.verifyApiResponseCode(userResponse, 201);
                
                    validApplicationRequest.Data.UserId = userResponse.body.data;          
                    cy.wait(5000);

                    //Make post request for eligibility check
                    cy.log(JSON.stringify(validApplicationRequest));
                    cy.apiRequest('POST', 'application', validApplicationRequest, token).then((response) => {
                        cy.log(JSON.stringify(response.body));
                        console.log('Application response:', response.body);
                    
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
        const applicationRequest = validApplicationRequestBody();
    
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('POST', '/user', validUserRequestBody(), token).then((userResponse) => {
                cy.verifyApiResponseCode(userResponse, 201);
    
                applicationRequest.Data.UserId = userResponse.body.data;
    
                cy.apiRequest('POST', 'application', applicationRequest, token).then((applicationResponse) => {
                    cy.log(JSON.stringify(applicationResponse.body));
                    console.log('Application response:', applicationResponse.body);
                
                    cy.verifyApiResponseCode(applicationResponse, 201);
                    cy.extractGuid(applicationResponse);
    
                    cy.get('@Guid').then((Guid) => {
                        cy.apiRequest('GET', `application/${Guid}`, {}).then((getResponse) => {
                            cy.verifyApiResponseCode(getResponse, 401);
                        });
                    });
                });
            });
        });
    });
})