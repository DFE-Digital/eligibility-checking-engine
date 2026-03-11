import { getandVerifyBearerToken } from '../../support/apiHelpers';
import {
    validLoginRequestBody,
    validEligibilityEventRequestBody,
    conflictingEligibilityEventRequestBody,
    missingDernEligibilityEventRequestBody,
} from '../../support/requestBodies';

// Each test run uses a unique HMRC event id to avoid cross-run pollution
const testHmrcEventId = () => `efe-test-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

describe('ECE - PUT /efe/api/v1/eligibility-events/{id}', () => {

    it('Returns 200 when a valid new eligibility event is created', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((response) => {
                cy.verifyApiResponseCode(response, 200);
            });
        });
    });

    it('Returns 200 when the same event id and same DERN are sent again (idempotent)', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            // First PUT — creates the record
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((firstResponse) => {
                cy.verifyApiResponseCode(firstResponse, 200);

                // Second PUT — same id, same DERN — should also return 200
                cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((secondResponse) => {
                    cy.verifyApiResponseCode(secondResponse, 200);
                });
            });
        });
    });

    it('Returns 409 when the same event id is sent with a different DERN', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            // Create the record first
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((createResponse) => {
                cy.verifyApiResponseCode(createResponse, 200);

                // Same id, different DERN — expect conflict
                cy.apiRequest('PUT', url, conflictingEligibilityEventRequestBody(), token).then((conflictResponse) => {
                    cy.verifyApiResponseCode(conflictResponse, 409);
                });
            });
        });
    });

    it('Returns 400 when required dern field is missing', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('PUT', url, missingDernEligibilityEventRequestBody(), token).then((response) => {
                cy.verifyApiResponseCode(response, 400);
            });
        });
    });

    it('Returns 400 when validityStartDate is on or after validityEndDate', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;
        const invalidDates = {
            eligibilityEvent: {
                ...validEligibilityEventRequestBody().eligibilityEvent,
                validityStartDate: '2026-04-23',
                validityEndDate: '2026-01-21', // End before start
            },
        };

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('PUT', url, invalidDates, token).then((response) => {
                cy.verifyApiResponseCode(response, 400);
            });
        });
    });

    it('Returns 200 and re-activates a previously deleted event (same id + same DERN)', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            // Create
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then(() => {
                // Soft-delete
                cy.apiRequest('DELETE', url, null, token).then((deleteResponse) => {
                    cy.verifyApiResponseCode(deleteResponse, 200);

                    // Re-create (same id, same DERN) — should reactivate
                    cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((reactivateResponse) => {
                        cy.verifyApiResponseCode(reactivateResponse, 200);
                    });
                });
            });
        });
    });
});

describe('ECE - DELETE /efe/api/v1/eligibility-events/{id}', () => {

    it('Returns 200 when an existing event is soft-deleted', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            // Create first
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then((createResponse) => {
                cy.verifyApiResponseCode(createResponse, 200);

                // Delete
                cy.apiRequest('DELETE', url, null, token).then((deleteResponse) => {
                    cy.verifyApiResponseCode(deleteResponse, 200);
                });
            });
        });
    });

    it('Returns 404 when the event does not exist', () => {
        const id = testHmrcEventId(); // Never created
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            cy.apiRequest('DELETE', url, null, token).then((response) => {
                cy.verifyApiResponseCode(response, 404);
            });
        });
    });

    it('Returns 404 when the event has already been deleted', () => {
        const id = testHmrcEventId();
        const url = `/efe/api/v1/eligibility-events/${id}`;

        getandVerifyBearerToken('/oauth2/token', validLoginRequestBody).then((token) => {
            // Create and delete
            cy.apiRequest('PUT', url, validEligibilityEventRequestBody(), token).then(() => {
                cy.apiRequest('DELETE', url, null, token).then((firstDelete) => {
                    cy.verifyApiResponseCode(firstDelete, 200);

                    // Second delete — should return 404
                    cy.apiRequest('DELETE', url, null, token).then((secondDelete) => {
                        cy.verifyApiResponseCode(secondDelete, 404);
                    });
                });
            });
        });
    });
});
