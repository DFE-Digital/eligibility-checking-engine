import { getAuthResponse } from '../../support/apiHelpers';
import { validSupportPortalLoginRequestBody } from '../../support/requestBodies';

describe('Support Portal Authorisation Tests', () => {
  it('Verify 200 response and User Id Is Returned when Valid Client Details are used', () => {
    getAuthResponse('/oauth2/token', validSupportPortalLoginRequestBody).then((response) => {
      // Ensure user_id is returned and is a valid GUID
      expect(response).to.have.property('user_id');
      const userId = response['user_id'];
      const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
      expect(userId).to.match(guidRegex);
    });
  });

  it('Ensure Cypress user has correct roles', () => {
    getAuthResponse('/oauth2/token', validSupportPortalLoginRequestBody).then((response) => {
      const userId = response['user_id'];
      cy.apiRequest('GET', `user/${userId}/roles`, {}, response.access_token).then((newResponse) => {
        // Assert the response 
        cy.verifyApiResponseCode(newResponse, 200)

        // Create missing default rules needed for Support Portal Cypress tests
        var roleName = "Support_CodeManagement";
        if (!newResponse.body.roles || newResponse.body.roles.length === 0 || !newResponse.body.roles.some((role: any) => role.name === roleName)) {
          cy.apiRequest('POST', `user/${userId}/roles`, { roleName }, response.access_token).then((createRoleResponse) => {
            cy.verifyApiResponseCode(createRoleResponse, 201);
          });
        }
        var roleName = "Support_UserManagement";
        if (!newResponse.body.roles || newResponse.body.roles.length === 0 || !newResponse.body.roles.some((role: any) => role.name === roleName)) {
          cy.apiRequest('POST', `user/${userId}/roles`, { roleName }, response.access_token).then((createRoleResponse) => {
            cy.verifyApiResponseCode(createRoleResponse, 201);
          });
        }
      });
    });
  });

  it('Get user roles by user ID', () => {
    getAuthResponse('/oauth2/token', validSupportPortalLoginRequestBody).then((response) => {
      const userId = response['user_id'];
      cy.apiRequest('GET', `user/${userId}/roles`, {}, response.access_token).then((newResponse) => {
        // Assert the response 
        cy.verifyApiResponseCode(newResponse, 200)

        // Ensure roles are returned and is an array
        expect(newResponse.body).to.have.property('data');
        expect(newResponse.body.data).to.be.an('array');
      });
    });
  });

  it('Add test role for Cypress support portal user', () => {
    getAuthResponse('/oauth2/token', validSupportPortalLoginRequestBody).then((response) => {
      const userId = response['user_id'];
      cy.apiRequest('POST', `user/${userId}/roles`, { RoleName: "Support_TestRole" }, response.access_token).then((createRoleResponse) => {
        cy.verifyApiResponseCode(createRoleResponse, 201);
      });
    });
  });

  it('Remove test role for Cypress support portal user', () => {
    getAuthResponse('/oauth2/token', validSupportPortalLoginRequestBody).then((response) => {
      const userId = response['user_id'];
      cy.apiRequest('DELETE', `user/${userId}/roles/Support_TestRole`, {}, response.access_token).then((deleteRoleResponse) => {
        cy.verifyApiResponseCode(deleteRoleResponse, 204);
      });
    });
  });
});
