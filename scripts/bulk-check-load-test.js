#!/usr/bin/env node
/**
 * Bulk Check Load Test Script
 * ───────────────────────────────────────────────────────────────────────────
 * Replicates the gateway timeout that occurs when a large batch of FSM
 * eligibility checks is submitted from the front end.
 *
 * Prerequisites:
 *   - API running locally  (dotnet run / F5)
 *   - Node.js 14+          (no npm install needed – uses built-in modules)
 *
 * Usage:
 *   node bulk-check-load-test.js [options]
 *
 * Options (submit mode):
 *   --count  <n>      Number of records to send  (default: 200)
 *   --type   <slug>   Check type slug            (default: free-school-meals)
 *                       free-school-meals | two-year-offer
 *                       early-year-pupil-premium | working-families
 *   --no-poll         Skip progress polling after submission
 *
 * Options (query mode — pass --id to skip submission entirely):
 *   --id     <guid>   Bulk check ID to query
 *   --progress        Show progress/status for the given ID
 *   --results         Show results for the given ID
 *   --take   <n>      Number of result rows to display  (default: 10)
 *   --nth    <n>      Display only the nth result (1-based)
 *
 * Examples:
 *   node bulk-check-load-test.js
 *   node bulk-check-load-test.js --count 1000
 *   node bulk-check-load-test.js --count 5000 --type free-school-meals
 *   node bulk-check-load-test.js --id abc-123 --progress
 *   node bulk-check-load-test.js --id abc-123 --results --take 20
 *   node bulk-check-load-test.js --id abc-123 --results --nth 42
 * ───────────────────────────────────────────────────────────────────────────
 */

'use strict';

const https = require('https');
const http = require('http');
const crypto = require('crypto');

// ─── Configuration ────────────────────────────────────────────────────────────
// Credentials and local authority ID come from appsettings.Development.json.
// controller happy without needing to pass an LA ID via meta if you prefer.
const CONFIG = {
  baseUrl: 'https://localhost:7117',  // Change to e.g. 'https://dev.eligibility-checking-engine.education.gov.uk' to query dev
  clientId: '',
  clientSecret: '',
  // Scope string sent in the token request — GenerateJSONWebToken only adds a
  // scope claim when this is present. 'local_authority' (no ID) satisfies
  // HasSingleScope; 'bulk_check' satisfies RequireBulkCheckScope.
  tokenScope: 'local_authority bulk_check',
  // Set to a valid LocalAuthorityID that exists in your local DB, or leave null
  // to omit it from the request (the field is nullable - bulk check works without it).
  localAuthorityId: 201,
  // Start at 200 to verify the full flow works, then escalate to find the timeout:
  //   node bulk-check-load-test.js --count 1000
  //   node bulk-check-load-test.js --count 2500
  //   node bulk-check-load-test.js --count 5000
  // NOTE: BulkEligibilityCheckLimit in appsettings.Development.json must be >= --count
  //       or the API returns 400 before it even touches the DB/queue.
  defaultRecordCount: 200,
  defaultType: 'free-school-meals',
  pollIntervalMs: 3000,
  pollTimeoutMs: 300_000,       // 5 min
};

// ─── Argument parsing ─────────────────────────────────────────────────────────
const argv = process.argv.slice(2);
let recordCount  = CONFIG.defaultRecordCount;
let checkType    = CONFIG.defaultType;
let doPoll       = true;
let queryId      = null;
let showProgress = false;
let showResults  = false;
let doRequeue    = false;
let takN         = 10;
let nthResult    = null;
let testingMode  = false;
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--count'    && argv[i + 1]) recordCount = parseInt(argv[++i], 10);
  else if (argv[i] === '--type'  && argv[i + 1]) checkType  = argv[++i];
  else if (argv[i] === '--no-poll') doPoll = false;
  else if (argv[i] === '--testing') testingMode = true;
  else if (argv[i] === '--id'    && argv[i + 1]) queryId    = argv[++i];
  else if (argv[i] === '--progress') showProgress = true;
  else if (argv[i] === '--results')  showResults  = true;
  else if (argv[i] === '--requeue')  doRequeue    = true;
  else if (argv[i] === '--take'  && argv[i + 1]) takN       = parseInt(argv[++i], 10);
  else if (argv[i] === '--nth'   && argv[i + 1]) nthResult  = parseInt(argv[++i], 10);
}

// ─── Data generation ──────────────────────────────────────────────────────────
// NI number prefixes that satisfy HMRC formatting rules:
//   - Neither letter can be D, F, I, Q, U, or V
//   - Second letter cannot be O
//   - Cannot start with BG, GB, NK, KN, TN, NT, ZZ
const NI_PREFIXES = [
  'AA','AB','AE','AH','AJ','AK','AL','AM','AP','AR','AS','AT','AW','AX','AY','AZ',
  'BA','BB','BE','BH','BJ','BK','BL','BM','BP','BR','BS','BT','BW','BX','BY','BZ',
  'CA','CB','CE','CH','CJ','CK','CL','CM','CP','CR','CS','CT','CW','CX','CY','CZ',
  'EA','EB','EE','EH','EJ','EK','EL','EM','EP','ER','ES','ET','EW','EX','EY','EZ',
  'HA','HB','HE','HH','HJ','HK','HL','HM','HP','HR','HS','HT','HW','HX','HY','HZ',
  'JA','JB','JC','JE','JH','JJ','JK','JL','JM','JP','JR','JS','JT','JW','JX','JY',
  'KA','KB','KE','KH','KJ','KK','KL','KM','KP','KR','KS','KT','KW','KX','KY',
  'LA','LB','LE','LH','LJ','LK','LL','LM','LP','LR','LS','LT','LW','LX','LY','LZ',
  'MA','MB','ME','MH','MJ','MK','ML','MM','MP','MR','MS','MT','MW','MX','MY','MZ',
  'NA','NB','NE','NH','NJ','NL','NM','NP','NR','NS','NW','NX','NY','NZ', // NT is prohibited
  'PA','PB','PC','PE','PH','PJ','PK','PL','PM','PP','PR','PS','PT','PW','PX','PY',
  'RA','RB','RE','RH','RJ','RK','RL','RM','RP','RR','RS','RT','RW','RX','RY','RZ',
  'SA','SB','SC','SE','SH','SJ','SK','SL','SM','SP','SR','SS','ST','SW','SX','SY','SZ',
  'TA','TB','TE','TH','TJ','TK','TL','TM','TP','TR','TS','TT','TW','TX','TY',
  'WA','WB','WE','WH','WJ','WK','WL','WM','WP','WR','WS','WT','WW','WX','WY',
  'XA','XB','XE','XH','XJ','XK','XL','XM','XP','XR','XS','XT','XW','XX','XY',
  'YA','YB','YE','YH','YJ','YK','YL','YM','YP','YR','YS','YT','YW','YX','YY',
];
// Prefixes used in --testing mode: map to predictable API outcomes without hitting DWP CAPI.
//   NN → Eligible, PN → NotEligible, RA → ParentNotFound  (XX omitted: slow to respond)
const TESTING_NI_PREFIXES = ['NN', 'PN', 'RA'];
const NI_SUFFIXES = ['A', 'B', 'C', 'D'];
const LAST_NAMES  = [
  'Smith','Jones','Williams','Taylor','Brown','Davies','Evans','Wilson','Thomas','Roberts',
  'Johnson','Lewis','Walker','Robinson','Wood','Thompson','White','Watson','Jackson','Wright',
  'Green','Harris','Cooper','King','Lee','Martin','Clarke','James','Morgan','Hughes',
  'Allen','Anderson','Bailey','Baker','Bell','Bennett','Carter','Clark','Collins','Cook',
  'Cox','Edwards','Foster','Gray','Hall','Hill','Mitchell','Morris','Parker','Phillips',
];

const rnd = (min, max) => crypto.randomInt(min, max + 1);

function generateNino(useTesting = false) {
  const pool   = useTesting ? TESTING_NI_PREFIXES : NI_PREFIXES;
  const prefix = pool[rnd(0, pool.length - 1)];
  const digits  = String(rnd(0, 999999)).padStart(6, '0');
  const suffix  = NI_SUFFIXES[rnd(0, 3)];
  return `${prefix}${digits}${suffix}`;
}

function generateDob() {
  // School-age children: roughly 4–18 years old
  const year  = rnd(2007, 2022);
  const month = String(rnd(1, 12)).padStart(2, '0');
  const day   = String(rnd(1, 28)).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function generateRecords(count, useTesting = false) {
  return Array.from({ length: count }, (_, i) => ({
    nationalInsuranceNumber: generateNino(useTesting),
    lastName: useTesting ? 'TESTER' : LAST_NAMES[rnd(0, LAST_NAMES.length - 1)],
    dateOfBirth: generateDob(),
    clientIdentifier: `LOAD-TEST-${String(i + 1).padStart(6, '0')}`,
  }));
}

// ─── HTTP helpers ─────────────────────────────────────────────────────────────
function rawRequest(method, url, body, contentType, extraHeaders = {}) {
  return new Promise((resolve, reject) => {
    const parsed  = new URL(url);
    const isHttps = parsed.protocol === 'https:';
    const proto   = isHttps ? https : http;
    const port    = parsed.port || (isHttps ? 443 : 80);

    const headers = { 'Content-Type': contentType, ...extraHeaders };
    if (body) headers['Content-Length'] = Buffer.byteLength(body);

    const isLoopback =
      parsed.hostname === 'localhost' ||
      parsed.hostname === '127.0.0.1' ||
      parsed.hostname === '::1';
    const allowInsecureTls = process.env.ALLOW_INSECURE_TLS === '1' && isLoopback;

    const options = {
      hostname: parsed.hostname,
      port,
      path: parsed.pathname + (parsed.search || ''),
      method,
      headers,
      rejectUnauthorized: !allowInsecureTls,
    };

    const req = proto.request(options, (res) => {
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const raw = Buffer.concat(chunks).toString();
        let json;
        try { json = JSON.parse(raw); } catch { json = raw; }
        resolve({ status: res.statusCode, body: json, raw });
      });
    });

    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

function postForm(url, fields) {
  const body = new URLSearchParams(fields).toString();
  return rawRequest('POST', url, body, 'application/x-www-form-urlencoded');
}

function postJson(url, payload, token) {
  const body = JSON.stringify(payload);
  return rawRequest('POST', url, body, 'application/json', { Authorization: `Bearer ${token}` });
}

function getJson(url, token) {
  return rawRequest('GET', url, null, 'application/json', { Authorization: `Bearer ${token}` });
}

// ─── Query helpers ────────────────────────────────────────────────────────────
async function queryProgress(token, id) {
  const url = `${CONFIG.baseUrl}/bulk-check/${id}/progress`;
  console.log(`\nProgress: GET ${url}`);
  const res = await getJson(url, token);
  if (res.status !== 200) {
    console.error(`HTTP ${res.status}`);
    console.error(JSON.stringify(res.body, null, 2));
    return;
  }
  console.log(JSON.stringify(res.body, null, 2));
}

async function queryResults(token, id) {
  const url = `${CONFIG.baseUrl}/bulk-check/${id}/`;
  console.log(`\nResults:  GET ${url}`);
  const res = await getJson(url, token);
  if (res.status !== 200) {
    console.error(`HTTP ${res.status}`);
    console.error(JSON.stringify(res.body, null, 2));
    return;
  }

  const items = res.body?.data ?? res.body ?? [];
  const total = Array.isArray(items) ? items.length : '?';
  console.log(`\nTotal records returned: ${total}`);

  if (nthResult !== null) {
    const item = Array.isArray(items) ? items[nthResult - 1] : null;
    if (!item) {
      console.error(`No result at position ${nthResult} (total: ${total})`);
      return;
    }
    console.log(`\n── Record ${nthResult} ──`);
    console.log(JSON.stringify(item, null, 2));
    return;
  }

  const slice = Array.isArray(items) ? items.slice(0, takN) : items;
  console.log(`\nShowing first ${Array.isArray(slice) ? slice.length : '?'} of ${total}:\n`);
  const display = Array.isArray(slice) ? slice : [slice];
  display.forEach((item, i) => {
    console.log(`── Record ${i + 1} ──`);
    console.log(JSON.stringify(item, null, 2));
  });
}

// ─── Requeue ──────────────────────────────────────────────────────────────────
// Fetches all results for a bulk check, finds those still queuedForProcessing,
// and drives each one through PUT /engine/process/{guid} directly, bypassing
// the Azure queue. Runs up to 10 concurrent requests at a time.
async function requeueStuck(token, id) {
  console.log(`\nFetching results for bulk check: ${id}`);
  const res = await getJson(`${CONFIG.baseUrl}/bulk-check/${id}/`, token);
  if (res.status !== 200) {
    console.error(`Failed to fetch results (HTTP ${res.status}):`);
    console.error(JSON.stringify(res.body, null, 2));
    return;
  }

  const items = res.body?.data ?? [];
  const stuck = items.filter(item => {
    const s = (item.status ?? item.Status ?? '').toLowerCase();
    return s === 'queuedforprocessing';
  });

  if (stuck.length === 0) {
    console.log('No stuck records found — all items have a terminal status.');
    return;
  }

  console.log(`Found ${stuck.length} stuck record(s) out of ${items.length} total. Processing...\n`);

  // Engine endpoint requires 'engine' scope — get a dedicated token for it.
  const engineScope = `${CONFIG.tokenScope} engine`.replace(/\bengine\b.*\bengine\b/, 'engine'); // dedupe
  const engineToken = await authenticate(engineScope);

  const CONCURRENCY = 10;
  let done = 0, succeeded = 0, failed = 0, stillQueued = 0;

  for (let i = 0; i < stuck.length; i += CONCURRENCY) {
    const batch = stuck.slice(i, i + CONCURRENCY);
    await Promise.all(batch.map(async (item) => {
      const guid = item.EligibilityCheckID ?? item.eligibilityCheckID ?? item.id ?? item.Id;
      if (!guid) { failed++; done++; return; }

      const url = `${CONFIG.baseUrl}/engine/process/${guid}`;
      try {
        const r = await rawRequest('PUT', url, null, 'application/json', { Authorization: `Bearer ${engineToken}` });
        if (r.status === 200) {
          succeeded++;
        } else if (r.status === 503) {
          stillQueued++; // still queuedForProcessing after engine call
        } else {
          failed++;
          if (r.status !== 404) console.error(`  GUID ${guid}: HTTP ${r.status}`);
        }
      } catch (e) {
        failed++;
        console.error(`  GUID ${guid}: ${e.message}`);
      }
      done++;
    }));

    if (done % 50 === 0 || done === stuck.length) {
      process.stdout.write(`\r  Progress: ${done}/${stuck.length} (ok=${succeeded} queued=${stillQueued} err=${failed})`);
    }
  }

  console.log(`\n\nRequeue complete.`);
  console.log(`  Processed  : ${done}`);
  console.log(`  Succeeded  : ${succeeded}`);
  console.log(`  StillQueued: ${stillQueued}`);
  console.log(`  Failed     : ${failed}`);
}

// ─── auth ─────────────────────────────────────────────────────────────────────
async function authenticate(scopeOverride) {
  console.log(`\n[1/3] Authenticating as '${CONFIG.clientId}'...`);
  const res = await postForm(`${CONFIG.baseUrl}/oauth2/token`, {
    grant_type:    'client_credentials',
    client_id:     CONFIG.clientId,
    client_secret: CONFIG.clientSecret,
    scope:         scopeOverride ?? CONFIG.tokenScope,
  });
  if (res.status !== 200) {
    throw new Error(`Auth failed (HTTP ${res.status}): ${JSON.stringify(res.body)}`);
  }
  const token = res.body?.access_token ?? res.body?.token;
  if (!token) throw new Error(`No access_token in response: ${JSON.stringify(res.body)}`);
  console.log(`      OK – token received.`);
  return token;
}

// ─── Submit ───────────────────────────────────────────────────────────────────
async function submitBulkCheck(token, records) {
  const endpoint = `${CONFIG.baseUrl}/bulk-check/${checkType}`;
  const meta = {
      filename:        `load-test-${records.length}.csv`,
      submittedBy:     'load-test-script@localhost',
    };
  if (CONFIG.localAuthorityId != null) meta.localAuthorityId = CONFIG.localAuthorityId;

  const payload  = {
    data: records,
    meta,
  };

  console.log(`\n[2/3] Submitting ${records.length} records`);
  console.log(`      POST ${endpoint}`);
  console.log(`      Payload size: ${(JSON.stringify(payload).length / 1024).toFixed(1)} KB`);

  const t0 = Date.now();
  let res;
  try {
    res = await postJson(endpoint, payload, token);
  } catch (err) {
    const elapsed = ((Date.now() - t0) / 1000).toFixed(1);
    console.error(`\n      ✗ REQUEST FAILED after ${elapsed}s`);
    console.error(`        ${err.message}`);
    if (err.message.includes('ECONNRESET') || err.message.includes('socket hang up')) {
      console.error('\n      ⚠  This looks like the gateway timeout — the server closed the');
      console.error('         connection before the bulk insert completed.');
    }
    process.exit(1);
  }

  const elapsed = ((Date.now() - t0) / 1000).toFixed(1);

  if (res.status === 200 || res.status === 202) {
    console.log(`      ✓ HTTP ${res.status} — responded in ${elapsed}s`);
    return res.body;
  }

  console.error(`      ✗ HTTP ${res.status} after ${elapsed}s`);
  console.error('      Response body:');
  console.error(JSON.stringify(res.body, null, 6));
  process.exit(1);
}

// ─── Progress polling ─────────────────────────────────────────────────────────
async function pollProgress(token, progressUrl) {
  const fullUrl  = progressUrl.startsWith('http') ? progressUrl : `${CONFIG.baseUrl}${progressUrl}`;
  const deadline = Date.now() + CONFIG.pollTimeoutMs;

  console.log(`\n[3/3] Polling progress: ${fullUrl}`);
  process.stdout.write('      ');

  while (Date.now() < deadline) {
    const t0  = Date.now();
    const res = await getJson(fullUrl, token);
    const status = res.body?.data?.status
      ?? Object.entries(res.body?.links ?? {}).find(([k]) => k.toLowerCase() === 'get_progress_check')?.[1]   // fallback
      ?? JSON.stringify(res.body).slice(0, 60);

    process.stdout.write(`[${status}] `);

    if (['Completed', 'Failed', 'Cancelled', 'Deleted'].includes(status)) {
      console.log(`\n      ✓ Final status: ${status}`);
      return status;
    }

    const wait = Math.max(0, CONFIG.pollIntervalMs - (Date.now() - t0));
    await new Promise((r) => setTimeout(r, wait));
  }

  console.log('\n      ⚠  Polling timed out (bulk check may still be running).');
  return 'polling-timeout';
}

// ─── Main ─────────────────────────────────────────────────────────────────────
(async () => {
  // ── Query mode: --id supplied ─────────────────────────────────────────────
  if (queryId) {
    const token = await authenticate();
    console.log(`\nQuerying bulk check ID: ${queryId}`);
    if (!showProgress && !showResults && !doRequeue) {
      // Default: show both
      showProgress = true;
      showResults  = true;
    }
    if (showProgress) await queryProgress(token, queryId);
    if (showResults)  await queryResults(token, queryId);
    if (doRequeue)    await requeueStuck(token, queryId);
    console.log('\nDone.');
    return;
  }

  // ── Submit mode ───────────────────────────────────────────────────────────
  console.log('╔════════════════════════════════════════╗');
  console.log('║      Bulk Check Load Test              ║');
  console.log('╚════════════════════════════════════════╝');
  console.log(`  API     : ${CONFIG.baseUrl}`);
  console.log(`  Client  : ${CONFIG.clientId}`);
  console.log(`  Scope   : ${CONFIG.tokenScope}`);
  console.log(`  Type    : ${checkType}`);
  console.log(`  Records : ${recordCount}`);
  console.log(`  LA ID   : ${CONFIG.localAuthorityId ?? '(not set)'}`);
  if (process.env.ALLOW_INSECURE_TLS === '1') {
    console.log('  TLS     : insecure override enabled for loopback hosts only');
  }
  if (testingMode) console.log('  Mode    : TESTING (lastName=TESTER, NI prefixes: NN/PN/RA)');

  const token   = await authenticate();
  const records = generateRecords(recordCount, testingMode);
  const resp    = await submitBulkCheck(token, records);

  console.log('\n      Response summary:');
  console.log(`        Status : ${resp?.data?.status ?? 'n/a'}`);
  const links = resp?.links ?? {};
  const progressUrl = Object.entries(links).find(([k]) => k.toLowerCase() === 'get_progress_check')?.[1];
  const bulkId = progressUrl?.split('/bulk-check/')?.[1]?.replace('/progress', '');
  if (bulkId) {
    console.log(`        Bulk check ID: ${bulkId}`);
    console.log(`\n  Re-query later with:`);
    console.log(`    node bulk-check-load-test.js --id ${bulkId} --progress`);
    console.log(`    node bulk-check-load-test.js --id ${bulkId} --results --take 20`);
    console.log(`    node bulk-check-load-test.js --id ${bulkId} --results --nth 5`);
  }

  if (doPoll && progressUrl) {
    await pollProgress(token, progressUrl);
  } else if (!progressUrl) {
    console.log('\n      (No progress link returned — cannot poll.)');
    console.log('      Full response:');
    console.log(JSON.stringify(resp, null, 4));
  }

  console.log('\nDone.');
})().catch((err) => {
  console.error('\nFatal:', err.message);
  process.exit(1);
});
