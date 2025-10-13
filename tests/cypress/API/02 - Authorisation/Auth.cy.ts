import { verifyUnauthorizedWithoutToken, getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBodyWithClientDetails, validLoginRequestBody } from '../../support/requestBodies';


describe('Authorisation Tests', () => {
  const invalidRequestBody = { lolzname: 'ecsUiUser', password: '123456' };
  const invalidClientDetails = "client_id=invalidClientId&client_secret=invalidSecret&scope=admin";
  const multipleLocalAuthorityScopes = "client_id=".concat(
    Cypress.env('JWT_USERNAME'),
    "&client_secret=",
    encodeURIComponent(Cypress.env('JWT_PASSWORD')),
    "&scope=local_authority:22 local_authority:41"
  );

  it('Verify 200 response and Bearer Token Is Returned when Valid Client Details are used', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBodyWithClientDetails).then((token) => {
    });
  });

  it('Verify 200 response and Bearer Token Is Returned when Valid Client Details with scope are used', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
    });
  });

  it('Verify 400 is returned with invalid credentials', () => {
    cy.apiRequest('POST', '/oauth2/token', invalidRequestBody, null, false, 'application/x-www-form-urlencoded').then((response) => {
      cy.verifyApiResponseCode(response, 400)
    });
  });

  it('Verify 401 is returned with invalid client details', () => {
    verifyUnauthorizedWithoutToken('POST', '/oauth2/token', invalidClientDetails, 'application/x-www-form-urlencoded');
  });

  it('Verify 401 is returned when multiple local authority scopes are requested', () => {
    cy.apiRequest('POST', '/oauth2/token', multipleLocalAuthorityScopes, null, false, 'application/x-www-form-urlencoded').then((response) => {
      cy.verifyApiResponseCode(response, 401)
    });
  });

  it('Verify 401 Unauthorized is returned when token is not provided for protected endpoints', () => {
    verifyUnauthorizedWithoutToken('POST', 'check/free-school-meals', invalidClientDetails, 'application/json');
    verifyUnauthorizedWithoutToken('POST', 'application', invalidClientDetails, 'application/json');
  });
});
