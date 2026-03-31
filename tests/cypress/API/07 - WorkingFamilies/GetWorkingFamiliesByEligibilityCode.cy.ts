// import { getandVerifyBearerToken } from "@/cypress/support/apiHelpers";
// import { validLoginRequestBody } from "@/cypress/support/requestBodies";


// const randomDateWithin7Days = () => {
//   const today = new Date();
//   const daysAgo = Math.floor(Math.random() * 7);
//   const dt = new Date(today);
//   dt.setDate(today.getDate() - daysAgo);
//   return dt.toISOString();
// }

// const generateEventsBlock = (reconfirmCount = 2) => {
//   const submissionDate = randomDateWithin7Days();

//   const block = [
//     {
//       event: 0,
//       record: { submissionDate }
//     }
//   ];

//   for (let i = 0; i < reconfirmCount; i++) {
//     block.push({
//       event: 1, // 1 being reconfirmation
//       record: { submissionDate }
//     });
//   }

//   return block;
// }

// const generateContiguousEventData = (blockCount = 3) => {
//   const blocks = [];

//   for (let i = 0; i < blockCount; i++) {
//     blocks.push(generateEventsBlock());
//   }

//   blocks.sort((a, b) =>
//     new Date(b[0].record.submissionDate).getTime() -
//     new Date(a[0].record.submissionDate).getTime()
//   );

//   return { data: blocks.flat() };
// }

// describe.skip("Get Working Family Events By Eligibility Code - Valid Contiguous Blocks", () => {

//   it("Verifies application then reconfirm events in correct contiguous block order", () => {

//     const eligibilityCode = 50009000005;

//     cy.intercept(
//       "GET",
//       `**/working-families-reporting/${eligibilityCode}`,
//       {
//         statusCode: 200,
//         body: generateContiguousEventData(3)
//       }
//     ).as("stubWFEvents");

//     getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
//       .then(token => {

//         cy.window()
//           .then(win =>
//             win.fetch(
//               `working-families-reporting/${eligibilityCode}`,
//               {
//                 method: "GET",
//                 headers: {
//                   Authorization: `Bearer ${token}`,
//                   "Content-Type": "application/json"
//                 }
//               }
//             )
//           )
//           .then(async (fetchRes) => {

//             const json = await fetchRes.json();

//             // shape it like Cypress response for compatibility
//             const res = {
//               status: fetchRes.status,
//               body: json
//             };

//             expect(res.status).to.eq(200);
//             expect(res.body.data).to.be.an("array").and.not.empty;

//             const events = res.body.data;

//             const blocks = [];
//             let currentBlock = [] as any[];

//             events.forEach((ev: any) => {
//               if (ev.event === 0) {
//                 if (currentBlock.length > 0) blocks.push(currentBlock);
//                 currentBlock = [ev];
//               } else {
//                 currentBlock.push(ev);
//               }
//             });
//             blocks.push(currentBlock);

//             blocks.forEach((block, index) => {
//               expect(block[0].event).to.eq(0);
//               block.slice(1).forEach(ev => {
//                 expect(ev.event).to.eq(1);
//               });
//             });

//             const appDates = blocks.map(b =>
//               new Date(b[0].record.submissionDate)
//             );
//             const sortedDesc = [...appDates].sort((a, b) => b - a);

//             expect(appDates).to.deep.equal(sortedDesc);

//           });
//     });
//   });

//   it("Single event → must return Application only", () => {
//     const eligibilityCode = "90012345671";

//     cy.intercept(
//       "GET",
//       `**/working-families-reporting/${eligibilityCode}`,
//       {
//         statusCode: 200,
//         body: {
//           data: [
//             {
//               event: 0,
//               record: { submissionDate: randomDateWithin7Days() }
//             }
//           ]
//         }
//       }
//     ).as("stubSingleEvent");

//     getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
//       .then(token => {

//         cy.window()
//           .then(win =>
//             win.fetch(
//               `working-families-reporting/${eligibilityCode}`,
//               {
//                 method: "GET",
//                 headers: {
//                   Authorization: `Bearer ${token}`
//                 }
//               }
//             )
//           )
//           .then(async (fetchRes) => {
//             const json = await fetchRes.json();

//             const res = {
//               status: fetchRes.status,
//               body: json
//             };

//             expect(res.status).to.eq(200);
//             expect(res.body.data.length).to.eq(1);
//             expect(res.body.data[0].event).to.eq(0);
//           });

//       });
//   });
// });

// describe.skip("Get Working Family Events By Eligibility Code - Invalid requests", () => {

//   it("Returns 400 when eligibility code does not exist", () => {
//     const eligibilityCode = "NOT_A_REAL_CODE";

//     cy.intercept(
//       "GET",
//       `**/working-families-reporting/${eligibilityCode}`,
//       {
//         statusCode: 400,
//         body: {
//           errors: [
//             { title: "No working family events found" }
//           ]
//         }
//       }
//     ).as("stubNotFound");

//     getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
//       .then(token => {

//         cy.window()
//           .then(win =>
//             win.fetch(
//               `working-families-reporting/${eligibilityCode}`,
//               {
//                 method: "GET",
//                 headers: { Authorization: `Bearer ${token}` }
//               }
//             )
//           )
//           .then(async (fetchRes) => {
//             const json = await fetchRes.json();

//             const res = { status: fetchRes.status, body: json };

//             expect(res.status).to.eq(400);
//             expect(res.body.errors?.[0].title).to.contain(
//               "No working family events found"
//             );
//           });

//       });
//   });

//   it("Returns 400 or 500 when backend throws an unexpected error", () => {
//     const eligibilityCode = "CAUSE_SERVER_ERROR";

//     cy.intercept(
//       "GET",
//       `**/working-families-reporting/${eligibilityCode}`,
//       {
//         statusCode: 500,
//         body: {
//           errors: [{ title: "Unexpected server error" }]
//         }
//       }
//     ).as("stubServerError");

//     getandVerifyBearerToken("/oauth2/token", validLoginRequestBody)
//       .then(token => {

//         cy.window()
//           .then(win =>
//             win.fetch(
//               `working-families-reporting/${eligibilityCode}`,
//               {
//                 method: "GET",
//                 headers: { Authorization: `Bearer ${token}` }
//               }
//             )
//           )
//           .then(async (fetchRes) => {
//             const json = await fetchRes.json();

//             const res = { status: fetchRes.status, body: json };

//             expect([400, 500]).to.include(res.status);
//             expect(res.body.errors?.[0].title).to.exist;
//           });

//       });
//   });
// });