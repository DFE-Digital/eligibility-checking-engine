/**
 * Rate Limit Test Script
 *
 * Two modes:
 *   USE_BULK_ENDPOINT = true  — POST to /bulk-check/free-school-meals
 *                               Each request counts as ITEMS_PER_REQUEST permits.
 *                               Requires the BulkCheck LA FK to exist in the DB.
 *   USE_BULK_ENDPOINT = false — POST individual requests to /check/free-school-meals
 *                               Each request counts as 1 permit. No FK constraint.
 *                               Send NUM_REQUESTS to exceed the limit.
 *
 * Usage:
 *   node test-rate-limit.js
 *
 * Configuration (edit the CONFIG block below as needed):
 *   BASE_URL            - API base URL
 *   CLIENT_ID           - OAuth2 client_id (must have a specific local_authority:XX scope)
 *   CLIENT_SECRET       - OAuth2 client_secret
 *   SCOPE               - Scopes to request in the token
 *   USE_BULK_ENDPOINT   - true = bulk endpoint (250 items/req), false = single endpoint (1 item/req)
 *   ITEMS_PER_REQUEST   - Items per bulk request (max 250, ignored when USE_BULK_ENDPOINT=false)
 *   NUM_REQUESTS        - Number of requests to send
 */

const https = require('https');
const http = require('http');

// ─── CONFIG ──────────────────────────────────────────────────────────────────

const CONFIG = {
  BASE_URL: 'https://localhost:7117',   // Change to http://localhost:XXXX if not using HTTPS locally
  CLIENT_ID: 'anoop',                  // Has local_authority:22 and bulk_check scopes — triggers rate limiting
  CLIENT_SECRET: 'Surf1ng_1',
  SCOPE: 'local_authority:201 bulk_check check',
  USE_BULK_ENDPOINT: true,            // false = single /check endpoint (no FK constraint, 1 permit/req)
                                       // true  = /bulk-check endpoint (250 permits/req, needs LA in DB)
  ITEMS_PER_REQUEST: 500,              // Max 250 — only used when USE_BULK_ENDPOINT=true
  NUM_REQUESTS: 4,                     // Requests to send (set > PermitLimit to trigger 429)
};

// ─── TEST DATA ────────────────────────────────────────────────────────────────
// Uses the TESTER test data configured in appsettings.Development.json:
//   TestData:LastName = "TESTER", NationalInsuranceNumber:Eligible prefix = "NN"

function buildBulkPayload(itemCount) {
  const data = [];
  for (let i = 0; i < itemCount; i++) {
    data.push({
      lastName: 'TESTER',
      dateOfBirth: '2000-01-15',
      nationalInsuranceNumber: `NN${String(i).padStart(6, '0')}A`,
    });
  }
  return { data };
}

function buildSinglePayload() {
  return { data: { lastName: 'TESTER', dateOfBirth: '2000-01-15', nationalInsuranceNumber: 'NN000000A' } };
}

// ─── HTTP HELPERS ─────────────────────────────────────────────────────────────

function request(method, path, body, headers = {}) {
  return new Promise((resolve, reject) => {
    const url = new URL(CONFIG.BASE_URL + path);
    const isHttps = url.protocol === 'https:';
    const lib = isHttps ? https : http;

    const bodyStr = body ? (typeof body === 'string' ? body : JSON.stringify(body)) : null;

    const options = {
      hostname: url.hostname,
      port: url.port || (isHttps ? 443 : 80),
      path: url.pathname + url.search,
      method,
      headers: {
        ...headers,
        ...(bodyStr ? { 'Content-Length': Buffer.byteLength(bodyStr) } : {}),
      },
      // Accept self-signed certs for local dev
      rejectUnauthorized: false,
    };

    const req = lib.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => (data += chunk));
      res.on('end', () => {
        try {
          resolve({ status: res.statusCode, headers: res.headers, body: JSON.parse(data) });
        } catch {
          resolve({ status: res.statusCode, headers: res.headers, body: data });
        }
      });
    });

    req.on('error', reject);
    if (bodyStr) req.write(bodyStr);
    req.end();
  });
}

async function getToken() {
  const formBody = new URLSearchParams({
    grant_type: 'client_credentials',
    client_id: CONFIG.CLIENT_ID,
    client_secret: CONFIG.CLIENT_SECRET,
    scope: CONFIG.SCOPE,
  }).toString();

  const res = await request('POST', '/oauth2/token', formBody, {
    'Content-Type': 'application/x-www-form-urlencoded',
  });

  if (res.status !== 200) {
    throw new Error(`Token request failed (${res.status}): ${JSON.stringify(res.body)}`);
  }

  return res.body.access_token;
}

// ─── MAIN ─────────────────────────────────────────────────────────────────────

async function run() {
  const isBulk = CONFIG.USE_BULK_ENDPOINT;
  const endpoint = isBulk ? '/bulk-check/free-school-meals' : '/check/free-school-meals';
  const itemsPerReq = isBulk ? CONFIG.ITEMS_PER_REQUEST : 1;

  console.log('=== Rate Limit Test ===');
  console.log(`Base URL:          ${CONFIG.BASE_URL}`);
  console.log(`Client:            ${CONFIG.CLIENT_ID}`);
  console.log(`Mode:              ${isBulk ? 'bulk' : 'single'} (${endpoint})`);
  console.log(`Items per request: ${itemsPerReq}`);
  console.log(`Requests to send:  ${CONFIG.NUM_REQUESTS}`);
  console.log(`Total permits:     ${itemsPerReq * CONFIG.NUM_REQUESTS}`);
  console.log('');

  // Step 1: Get token
  process.stdout.write('Fetching token... ');
  const token = await getToken();
  console.log('OK\n');

  const payload = isBulk ? buildBulkPayload(CONFIG.ITEMS_PER_REQUEST) : buildSinglePayload();

  // Step 2: Send requests
  for (let i = 1; i <= CONFIG.NUM_REQUESTS; i++) {
    process.stdout.write(`Request ${i}/${CONFIG.NUM_REQUESTS} (${itemsPerReq} permit${itemsPerReq > 1 ? 's' : ''})... `);

    const res = await request('POST', endpoint, payload, {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    });

    const retryAfter = res.headers['retry-after'];

    if (res.status === 202) {
      console.log(`✓ 202 Accepted`);
    } else if (res.status === 429) {
      console.log(`✗ 429 Too Many Requests  (Retry-After: ${retryAfter ?? 'not set'}s)`);
      console.log('\n  Rate limit hit as expected. Stopping.');
      break;
    } else {
      console.log(`? ${res.status} - ${JSON.stringify(res.body)}`);
    }
  }

  console.log('\nDone.');
}

run().catch((err) => {
  console.error('\nError:', err.message);
  process.exit(1);
});
