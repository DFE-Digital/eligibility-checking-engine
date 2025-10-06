import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody, validHMRCRequestBody, validWorkingFamiliesRequestBody, validWorkingFamiliesRequestBodyEligible }
    from '../../support/requestBodies';


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

    
    it('Verify 200 Success response is returned with valid guid Working Families notFound',() => {
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
                        cy.verifyGetEligibilityWFCheckResponseDataNotFound(newResponse, requestBody)
                    })
                });
            });
        });
    })
});
    it('Verify 200 Success response is returned with valid guid Working Families found',function () {
                if (Cypress.env('ENV') === 'PP') {
                    this.skip();
                }
        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            //Make post request for eligibility check
            cy.log(Cypress.env('lastName'));
            const requestBody = validWorkingFamiliesRequestBodyEligible();
            cy.apiRequest('POST', 'check/working-families', requestBody, token).then((response) => {
                cy.verifyApiResponseCode(response, 202);
                //extract Guid
                cy.extractGuid(response);
                cy.wait(5000);
                //make get request using the guid 
                cy.get('@Guid').then((Guid) => {
                    cy.apiRequest('GET', `check/${Guid}`, {}, token).then((newResponse) => {
                        // Assert the response 
                        cy.verifyApiResponseCode(newResponse, 200)
                        cy.verifyGetEligibilityWFCheckResponseDataFound(newResponse, requestBody)
                    })
                });
            });
        });
    })
    it('Verify 404 Not Found response is returned with invalid guid', () => {
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('GET', 'check/7fc12dc9-5a9d-4155-887c-d2b3d60384e', {}, token).then((response) => {
                cy.verifyApiResponseCode(response, 404)
            });
        });
    });

describe('GET eligibility soft check by Guid and Type', () => {
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
                    cy.apiRequest('GET', `check/FreeSchoolMeals/${Guid}`, {}, token).then((newResponse) => {
                        // Assert the response 
                        cy.verifyApiResponseCode(newResponse, 200)
                        cy.verifyGetEligibilityCheckResponseData(newResponse, requestBody)
                    })
                });
            });
        });
    })
    it('Verify 404 Not Found response is returned with correct type but invalid guid', () => {
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('GET', 'check/FreeSchoolMeals/7fc12dc9-5a9d-4155-887c-d2b3d60384e', {}, token).then((response) => {
                cy.verifyApiResponseCode(response, 404);
                expect(response.body).to.have.property("errors");
            });
        });
    });
    it('Verify 404 Not Found response is returned with incorrect type but valid guid', () => {
        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            const requestBody = validHMRCRequestBody();
            cy.apiRequest('POST', 'check/free-school-meals', requestBody, token).then((response) => {
                cy.verifyApiResponseCode(response, 202);
                //extract Guid
                cy.extractGuid(response);
                //make get request using the guid 
                cy.get('@Guid').then((Guid) => {
                    cy.apiRequest('GET', `check/WorkingFamilies/${Guid}`, {}, token).then((response) => {
                        cy.verifyApiResponseCode(response, 404);
                        expect(response.body).to.have.property("errors");
                    });
                });
            });
        });
    });
});