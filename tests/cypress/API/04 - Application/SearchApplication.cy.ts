// /FreeSchoolMeals/Application/Search

import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody } from '../../support/requestBodies';
import { ApplicationData } from '../../support/interfaces';


describe('Search Application', () => {

    const body = {

        pageNumber: 1,
        pageSize: 50000,
        data: {
            localAuthority: null,
            establishment: 123456,
            status: null,
            parentLastName: null,
            parentNationalInsuranceNumber: null,
            parentNationalAsylumSeekerServiceNumber: null,
            parentDateOfBirth: null,
            childLastName: null,
            childDateOfBirth: null
        }

    }

    const expectedApplicationsData: ApplicationData[] = [
        {
            id: "bf96e60e-2030-4682-9742-0bd97787d6e2",
            reference: "62719512",
            establishment: {
                id: 100020,
                name: "Primrose Hill School",
                localAuthority: {
                    id: 202,
                    name: "Camden"
                }
            },
            parentFirstName: "Homer",
            parentLastName: "Simpson",
            parentNationalInsuranceNumber: "AB123456C",
            parentNationalAsylumSeekerServiceNumber: "",
            parentDateOfBirth: "1985-01-01",
            childFirstName: "Tom",
            childLastName: "sdf",
            childDateOfBirth: "2001-01-01",
            status: "Open",
            user: null
        }
    ];

    it('Verify 200 Success response is returned with valid search data', () => {

        //Get token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            //Make post request for eligibility check
            cy.apiRequest('POST', 'application/search', body, token).then((response) => {
                // Assert the status and statusText
                cy.log(JSON.stringify(response));
                cy.verifyApiResponseCode(response, 200);
                cy.verifyApplicationSearchResponse(response, expectedApplicationsData)
            })
        })
    })

})