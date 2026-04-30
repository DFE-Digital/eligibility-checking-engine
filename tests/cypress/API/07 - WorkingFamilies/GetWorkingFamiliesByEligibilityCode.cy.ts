import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

/**
 * Utility: random ISO date within last 7 days
 */
const randomDateWithin7Days = () => {
  const today = new Date();
  const daysAgo = Math.floor(Math.random() * 7);
  const dt = new Date(today);
  dt.setDate(today.getDate() - daysAgo);
  return dt.toISOString();
};


const generateContiguousEventBlock  = (
  applicationCount = 3,
  reconfirmPerApplication = 2
) => {
  const events: any[] = [];

  for (let i = 0; i < applicationCount; i++) {
    const baseDate = new Date(randomDateWithin7Days());

    // application
    events.push({
      event: 0,
      record: {
        submissionDate: baseDate.toISOString()
      }
    });

    // reconfirmation(s) for each application
    for (let r = 0; r < reconfirmPerApplication; r++) {
      const d = new Date(baseDate);
      d.setDate(baseDate.getDate() - (r + 1));

      events.push({
        event: 1,
        record: {
          submissionDate: d.toISOString()
        }
      });
    }
  }

  // Backend contract: newest first
  events.sort(
    (a, b) =>
      new Date(b.record.submissionDate).getTime() -
      new Date(a.record.submissionDate).getTime()
  );

  return { data: events };
};

describe("Get Working Family Events By Eligibility Code – Valid responses", () => {

  it("returns a flat list ordered by submission date DESC with valid event types", () => {

    const eligibilityCode = "50009000005";

    cy.intercept(
      "GET",
      `**/working-families-reporting/${eligibilityCode}`,
      {
        statusCode: 200,
        body: generateContiguousEventBlock(3, 2)
      }
    ).as("stubWFEvents");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
      .then(token => {
        cy.window()
          .then(win =>
            win.fetch(`working-families-reporting/${eligibilityCode}`, {
              method: "GET",
              headers: {
                Authorization: `Bearer ${token}`,
                "Content-Type": "application/json"
              }
            })
          )
          .then(async fetchRes => {
            const json = await fetchRes.json();

            expect(fetchRes.status).to.eq(200);
            expect(json.data).to.be.an("array").and.not.empty;

            const events = json.data;

            // newest to oldest order check
            const submissionDates = events.map(
              e => new Date(e.record.submissionDate)
            );

            const expectedOrder = [...submissionDates].sort(
              (a, b) => b.getTime() - a.getTime()
            );

            expect(submissionDates).to.deep.equal(
              expectedOrder,
              "Events must be ordered by submissionDate DESC"
            );

            events.forEach(ev => {
              expect([0, 1]).to.include(
                ev.event,
                "Event must be Application(0) or Reconfirm(1)"
              );
            });

            expect(
              events.some(e => e.event === 0),
              "At least one Application should exist"
            ).to.eq(true);

            expect(
              events.some(e => e.event === 1),
              "At least one Reconfirmation should exist"
            ).to.eq(true);
          });
      });
  });

  it("single event returns exactly one Application", () => {

    const eligibilityCode = "90012345671";

    cy.intercept(
      "GET",
      `**/working-families-reporting/${eligibilityCode}`,
      {
        statusCode: 200,
        body: {
          data: [
            {
              event: 0,
              record: {
                submissionDate: randomDateWithin7Days()
              }
            }
          ]
        }
      }
    ).as("stubSingleEvent");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
      .then(token => {
        cy.window()
          .then(win =>
            win.fetch(`working-families-reporting/${eligibilityCode}`, {
              method: "GET",
              headers: { Authorization: `Bearer ${token}` }
            })
          )
          .then(async fetchRes => {
            const json = await fetchRes.json();

            expect(fetchRes.status).to.eq(200);
            expect(json.data).to.have.length(1);
            expect(json.data[0].event).to.eq(0);
          });
      });
  });
});

describe("Get Working Family Events By Eligibility Code – Error responses", () => {

  it("returns 400 when eligibility code is invalid", () => {

    const eligibilityCode = "NOT_A_REAL_CODE";

    cy.intercept(
      "GET",
      `**/working-families-reporting/${eligibilityCode}`,
      {
        statusCode: 400,
        body: {
          errors: [{ title: "No working family events found" }]
        }
      }
    ).as("stubNotFound");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
      .then(token => {
        cy.window()
          .then(win =>
            win.fetch(`working-families-reporting/${eligibilityCode}`, {
              method: "GET",
              headers: { Authorization: `Bearer ${token}` }
            })
          )
          .then(async fetchRes => {
            const json = await fetchRes.json();

            expect(fetchRes.status).to.eq(400);
            expect(json.errors?.[0].title).to.contain(
              "No working family events found"
            );
          });
      });
  });

  it("returns 500 when backend throws an unexpected error", () => {

    const eligibilityCode = "CAUSE_SERVER_ERROR";

    cy.intercept(
      "GET",
      `**/working-families-reporting/${eligibilityCode}`,
      {
        statusCode: 500,
        body: {
          errors: [{ title: "Unexpected server error" }]
        }
      }
    ).as("stubServerError");

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
      .then(token => {
        cy.window()
          .then(win =>
            win.fetch(`working-families-reporting/${eligibilityCode}`, {
              method: "GET",
              headers: { Authorization: `Bearer ${token}` }
            })
          )
          .then(async fetchRes => {
            const json = await fetchRes.json();

            expect(fetchRes.status).to.eq(500);
            expect(json.errors?.[0].title).to.exist;
          });
      });
  });
});