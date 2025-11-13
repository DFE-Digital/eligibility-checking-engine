import { getandVerifyBearerToken } from '../../support/apiHelpers';
import { validLoginRequestBody } from '../../support/requestBodies';

describe('Testing the API', function () {
    it('Receives valid FormData and processes the information correctly', function () {
        // Declarations
        const fileName = 'MAT and Acadmies - Group Data.csv'; // File name including extension
        const method = 'POST';
        const url = Cypress.config('baseUrl') + 'admin/import-multi-academy-trusts';
        const fileType = 'text/csv'; // CSV file type     
        const expectedAnswer = '{"data":"MAT and Acadmies - Group Data.csv - MAT File Processed."}';


        // Get file from fixtures as binary
        cy.fixture(fileName, 'binary').then((excelBin: string) => {
            // File in binary format gets converted to blob so it can be sent as Form data
            const blob = Cypress.Blob.binaryStringToBlob(excelBin, fileType);

            // Build up the form
            const formData = new FormData();
            formData.set('file', blob, fileName); // Adding a file to the form

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
});
