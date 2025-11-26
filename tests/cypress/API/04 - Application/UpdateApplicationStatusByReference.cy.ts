// /FreeSchoolMeals/Application/Search

import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody, validApplicationRequestBody, validApplicationSupportRequestBody, validUserRequestBody } from '../../support/requestBodies';


describe('Update Application Status By Reference', () => {
  const validBaseApplicationRequest = validApplicationRequestBody();
    
    it('Verify 200 Success response is returned when updating by reference', () => {

      getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
          const requestBody = validApplicationSupportRequestBody();
          cy.apiRequest('POST', 'check/free-school-meals', requestBody, token).then((response) => {
              cy.verifyApiResponseCode(response, 202);
              cy.apiRequest('POST', '/user', validUserRequestBody(), token).then((response) => {
                  validBaseApplicationRequest.Data.UserId = response.Data;
                  cy.wait(60000);

                  cy.apiRequest('POST', 'application', validBaseApplicationRequest, token).then((response) => {
                      // Assert the status and statusText
                      cy.verifyApiResponseCode(response, 201);

                      // Assert the response body data
                      cy.verifyPostApplicationResponse(response, validBaseApplicationRequest);

                      const reference = response.body.data.reference;

                      const updatedRequestBody = {
                          "data": {
                              status: "EvidenceNeeded",
                              establishmentUrn: 123456
                          }
                      };

                      cy.apiRequest('PATCH', `application/reference/${reference}`, updatedRequestBody, token).then((patchResponse) => {
                          cy.verifyApiResponseCode(patchResponse, 200);

                          expect(patchResponse.body.data).to.have.property('status', 'EvidenceNeeded');
                          expect(patchResponse.body.data).to.have.property('establishmentUrn', 123456);
                      })
                  });
              });
          });
      });
    })


})
