// cypress/support/apiHelpers.ts

export function buildUrl(path: string): string {
    const baseUrl = Cypress.config('baseUrl') || '';
    return new URL(path, baseUrl.endsWith('/') ? baseUrl : baseUrl + '/').href;
}

export const verifyUnauthorizedWithoutToken = (method: string, endpoint: string, requestBody: any, contentType: string) => {
    cy.apiRequest(method, endpoint, requestBody, null, null, contentType).then((response) => {
        cy.verifyApiResponseCode(response, 401)
    });
}


export const getandVerifyBearerToken = (url: string, requestBody: any): Cypress.Chainable<string> => {
    cy.log(requestBody);
    return cy.apiRequest('POST', url, requestBody, null, null, 'application/x-www-form-urlencoded').then((response) => {
        expect(response.status).to.equal(200);
        expect(response.body).to.have.property('access_token');
        expect(response.body).to.have.property('expires_in')
        
        return response.body.access_token;
    });
};