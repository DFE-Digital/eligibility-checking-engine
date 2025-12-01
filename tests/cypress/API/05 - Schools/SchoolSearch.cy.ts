import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody } from '../../support/requestBodies';

describe('Verify School Search', () => {

  const expectedSchoolData = {
    "id": 143409,
    "name": "Roselands Primary School",
    "postcode": "EN11 9AR",
    "street": "High Wood Road",
    "locality": "",
    "town": "Hoddesdon",
    "county": "Hertfordshire",
    "la": "Hertfordshire",
    "distance": 0.0

  };
  const searchCriteria = 'Roselands Primary School';


  it('Verify 200 OK and Bearer Token Is Returned when Valid Credentials are used', () => {
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('GET', `establishment/search?query=${searchCriteria}`, {}, token).then((response) => {
        cy.verifyApiResponseCode(response, 200)
        cy.verifySchoolSearchResponse(response, expectedSchoolData);
      })
    });
  });

  it('Verify 400 response is returned for invalid search criteria', () => {
    const invalidSearchCriteria = 'ab'
    getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
      cy.apiRequest('GET', `establishment/search?query=${invalidSearchCriteria}`, {}, token).then((response) => {
        cy.verifyApiResponseCode(response, 400)

      })
    });
  });


  it('Verify 401 response is returned when bearer token is not provided', () => {
    cy.apiRequest('GET', `establishment/search?query=${searchCriteria}`, {},'application/x-www-form-urlencoded').then((response) => {
      cy.verifyApiResponseCode(response, 401)
    });
  });
})  