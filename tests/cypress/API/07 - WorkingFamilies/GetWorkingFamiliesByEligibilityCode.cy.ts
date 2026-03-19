import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
import { validLoginRequestBody } from "@/cypress/support/requestBodies";

describe("Get Working Family Events By Eligibility Code - Valid Contiguous Blocks", () => {
  it("Verifies application then reconfirm events in correct contiguous block order", () => {
    const eligibilityCode = 50009000005;

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `working-families-reporting/${eligibilityCode}`,
          null,
          token,
        ).then((res) => {
          expect(res.status).to.eq(200);
          expect(res.body).to.have.property("data");

          const events = res.body.data;
          expect(events.length).to.be.greaterThan(0);

          const blocks: any[] = [];
          let currentBlock: any[] = [];

          events.forEach((ev: any) => {
            if (ev.event === 0) {
              // Application
              if (currentBlock.length > 0) blocks.push(currentBlock);
              currentBlock = [ev];
            } else {
              currentBlock.push(ev);
            }
          });
          blocks.push(currentBlock); // final block

          blocks.forEach((block, index) => {
            // Application must be first
            expect(
              block[0].event,
              `Block ${index} should start with Application`,
            ).to.eq(0);

            //All following events should be reconfirms
            block.slice(1).forEach((ev : any) => {
              expect(ev.event).to.eq(1);
            });
          });

          const appDates = blocks.map(
            (b) => new Date(b[0].record.submissionDate),
          );
          // copy of dates, sorted by times
          const sortedDesc = [...appDates].sort(
            (a, b) => b.getTime() - a.getTime(),
          );

          expect(appDates).to.deep.equal(sortedDesc);
        });
      },
    );
  });

  it("Single event → must return Application only", () => {
    const eligibilityCode = "90012345671";

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then((token) => {
      cy.apiRequest(
        "GET",
        `working-families-reporting/${eligibilityCode}`,
        null,
        token
      ).then((res) => {

        expect(res.status).to.eq(200);
        expect(res.body.data.length).to.eq(1);

        expect(res.body.data[0].event).to.eq(0);  // Application
      });
    });
  });
});

describe("Get Working Family Events By Eligibility Code - Invalid requests", () => {
  it("Returns 400 when eligibility code does not exist", () => {
    const eligibilityCode = "NOT_A_REAL_CODE";

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then(
      (token) => {
        cy.apiRequest(
          "GET",
          `working-families-reporting/${eligibilityCode}`,
          null,
          token,
        ).then((res) => {
          expect(res.status).to.eq(400);
          expect(res.body.errors?.[0].title).to.contain(
            "No working family events found",
          );
        });
      },
    );
  });

  it("Returns 400 or 500 when backend throws an unexpected error", () => {
    const eligibilityCode = "CAUSE_SERVER_ERROR"; 

    getandVerifyBearerToken("/oauth2/token", validLoginRequestBody).then((token) => {

      cy.apiRequest(
        "GET",
        `working-families-reporting/${eligibilityCode}`,
        null,
        token
      ).then((res) => {

        expect([400, 500]).to.include(res.status);
        expect(res.body.errors?.[0].title).to.exist;
      });
    });
  });

});
