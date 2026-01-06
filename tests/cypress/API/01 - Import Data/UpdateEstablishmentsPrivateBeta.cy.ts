import { buildUrl, getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody } from '../../support/requestBodies';

describe('Update Establishments Private Beta API', function () {

    it('Receives valid FormData and updates establishments private beta correctly', function () {
        // Declarations
        const fileName = 'EstablishmentPrivateBeta.csv';
        const method = 'POST';
        const url = buildUrl('admin/update-establishments-private-beta');
        const fileType = 'text/csv';
        const expectedAnswer = '{"data":"EstablishmentPrivateBeta.csv - Establishment Private Beta Updated."}';

        // Get file from fixtures as binary
        cy.fixture(fileName, 'binary').then((csvBin: string) => {
            // File in binary format gets converted to blob so it can be sent as Form data
            const blob = Cypress.Blob.binaryStringToBlob(csvBin, fileType);

            // Build up the form
            const formData = new FormData();
            formData.set('file', blob, fileName);

            // Get Bearer token
            getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token: string) => {
                // Perform the request
                cy.form_request(method, url, formData, token, (response: XMLHttpRequest) => {
                    expect(response.status).to.eq(200);
                    expect(expectedAnswer).to.eq(response.response);
                });
            });
        });
    });

    it('Returns 401 when no bearer token is provided', function () {
        // Declarations
        const fileName = 'EstablishmentPrivateBeta.csv';
        const method = 'POST';
        const url = buildUrl('admin/update-establishments-private-beta');
        const fileType = 'text/csv';

        // Get file from fixtures as binary
        cy.fixture(fileName, 'binary').then((csvBin: string) => {
            // File in binary format gets converted to blob so it can be sent as Form data
            const blob = Cypress.Blob.binaryStringToBlob(csvBin, fileType);

            // Build up the form
            const formData = new FormData();
            formData.set('file', blob, fileName);

            // Perform the request without token
            cy.form_request(method, url, formData, null, (response: XMLHttpRequest) => {
                expect(response.status).to.eq(401);
            });
        });
    });

    it('Returns 400 when invalid file content is provided', function () {
        // Declarations
        const method = 'POST';
        const url = buildUrl('admin/update-establishments-private-beta');
        const fileType = 'text/csv';
        const invalidContent = 'InvalidHeader1,InvalidHeader2\nvalue1,value2';

        // Create invalid CSV blob
        const blob = new Blob([invalidContent], { type: fileType });

        // Build up the form
        const formData = new FormData();
        formData.set('file', blob, 'invalid.csv');

        // Get Bearer token
        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token: string) => {
            // Perform the request
            cy.form_request(method, url, formData, token, (response: XMLHttpRequest) => {
                expect(response.status).to.eq(400);
            });
        });
    });
});
