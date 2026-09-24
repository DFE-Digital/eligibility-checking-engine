#!/usr/bin/env node
/**
 * Bulk Check CSV Generator
 * ────────────────────────────────────────────────────────────────
 * Generates a CSV in the exact format expected by the front-end
 * bulk check upload.
 *
 * Usage:
 *   node generate-bulk-check-csv.js [options]
 *
 * Options:
 *   --count   <n>      Number of data rows to generate  (default: 500)
 *   --outFile <path>   Output file path  (default: C:\Test Data\bulk-check-test.csv)
 *   --mode    <mode>   CSV format mode   (default: "default")
 *
 * Modes:
 *   default   Headers: Parent National Insurance number, Parent asylum support
 *                      reference number, Parent Date of Birth, Parent Last Name
 *   FSMB      Headers: Parent First Name, Parent Last Name, Parent Date of Birth,
 *                      Parent National Insurance Number, Parent asylum seeker
 *                      reference number
 *
 * Examples:
 *   node generate-bulk-check-csv.js
 *   node generate-bulk-check-csv.js --count 5000
 *   node generate-bulk-check-csv.js --count 1000 --mode FSMB --outFile "C:\Test Data\fsmb.csv"
 * ────────────────────────────────────────────────────────────────
 */

'use strict';

const fs   = require('fs');
const path = require('path');

// ─── Argument parsing ─────────────────────────────────────────────────────────
const argv = process.argv.slice(2);
let count       = 500;
let outFile     = 'C:\\Test Data\\bulk-check-test.csv';
let mode        = 'default';
let testingMode = false;
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--count'   && argv[i + 1]) count   = parseInt(argv[++i], 10);
  if ((argv[i] === '--out' || argv[i] === '--outFile') && argv[i + 1]) outFile = argv[++i];
  if (argv[i] === '--mode'    && argv[i + 1]) mode    = argv[++i];
  if (argv[i] === '--testing') testingMode = true;
}

// ─── Shared data tables ───────────────────────────────────────────────────────
// Valid NI prefixes (all prohibited pairs removed: BG GB NK KN TN NT ZZ)
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
  'NA','NB','NE','NH','NJ','NL','NM','NP','NR','NS','NW','NX','NY','NZ', // NT prohibited
  'PA','PB','PC','PE','PH','PJ','PK','PL','PM','PP','PR','PS','PT','PW','PX','PY',
  'RA','RB','RE','RH','RJ','RK','RL','RM','RP','RR','RS','RT','RW','RX','RY','RZ',
  'SA','SB','SC','SE','SH','SJ','SK','SL','SM','SP','SR','SS','ST','SW','SX','SY','SZ',
  'TA','TB','TE','TH','TJ','TK','TL','TM','TP','TR','TS','TT','TW','TX','TY',
  'WA','WB','WE','WH','WJ','WK','WL','WM','WP','WR','WS','WT','WW','WX','WY',
  'XA','XB','XE','XH','XJ','XK','XL','XM','XP','XR','XS','XT','XW','XX','XY',
  'YA','YB','YE','YH','YJ','YK','YL','YM','YP','YR','YS','YT','YW','YX','YY',
];
const NI_SUFFIXES = ['A', 'B', 'C', 'D'];

// Prefixes used in --testing mode: map to predictable API outcomes without hitting DWP CAPI.
//   NN → Eligible, PN → NotEligible, RA → ParentNotFound  (XX omitted: slow to respond)
const TESTING_NI_PREFIXES = ['NN', 'PN', 'RA'];

const FIRST_NAMES = [
  'James','Oliver','Harry','Jack','George','Noah','Charlie','Jacob','Alfie','Freddie',
  'Olivia','Amelia','Isla','Ava','Mia','Isabella','Sophie','Poppy','Emily','Lily',
  'Mohammed','Aisha','Fatima','Omar','Layla','Yusuf','Zara','Ibrahim','Sara','Ali',
];
const LAST_NAMES = [
  'Smith','Jones','Williams','Taylor','Brown','Davies','Evans','Wilson','Thomas','Roberts',
  'Johnson','Lewis','Walker','Robinson','Wood','Thompson','White','Watson','Jackson','Wright',
  'Green','Harris','Cooper','King','Lee','Martin','Clarke','James','Morgan','Hughes',
  'Allen','Anderson','Bailey','Baker','Bell','Bennett','Carter','Clark','Collins','Cook',
  'Cox','Edwards','Foster','Gray','Hall','Hill','Mitchell','Morris','Parker','Phillips',
  'Price','Rogers','Turner','Ward','Webb','West','Young','Adams','Campbell','Shaw',
];

// ─── Generators ───────────────────────────────────────────────────────────────
const rnd = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;

const generateNino = (useTesting = false) => {
  const pool = useTesting ? TESTING_NI_PREFIXES : NI_PREFIXES;
  return `${pool[rnd(0, pool.length - 1)]}${String(rnd(0, 999999)).padStart(6, '0')}${NI_SUFFIXES[rnd(0, 3)]}`;
};

const generateDob = () =>
  `${rnd(1970, 2001)}-${String(rnd(1, 12)).padStart(2, '0')}-${String(rnd(1, 28)).padStart(2, '0')}`;

const pick = (arr) => arr[rnd(0, arr.length - 1)];

// ─── Mode definitions ─────────────────────────────────────────────────────────
// To add a new mode: add an entry here with a `header` string and a `row` function.
const MODES = {
  default: {
    header: 'Parent National Insurance number,Parent asylum support reference number,Parent Date of Birth,Parent Last Name',
    row: (useTesting) => {
      // NASS left blank — NI and NASS are mutually exclusive
      return `${generateNino(useTesting)},,${generateDob()},${useTesting ? 'TESTER' : pick(LAST_NAMES)}`;
    },
  },

  FSMB: {
    header: 'Parent First Name,Parent Last Name,Parent Date of Birth,Parent National Insurance Number,Parent asylum seeker reference number',
    row: (useTesting) => {
      // Asylum seeker reference number left blank — mutually exclusive with NI
      return `${useTesting ? 'TEST' : pick(FIRST_NAMES)},${useTesting ? 'TESTER' : pick(LAST_NAMES)},${generateDob()},${generateNino(useTesting)},`;
    },
  },
};

// ─── CSV generation ───────────────────────────────────────────────────────────
function buildCsv(rowCount, modeDef, useTesting) {
  const lines = [modeDef.header];
  for (let i = 0; i < rowCount; i++) {
    lines.push(modeDef.row(useTesting));
  }
  return lines.join('\r\n');
}

// ─── Main ─────────────────────────────────────────────────────────────────────
const modeDef = MODES[mode];
if (!modeDef) {
  console.error(`Unknown mode "${mode}". Available modes: ${Object.keys(MODES).join(', ')}`);
  process.exit(1);
}

const csv     = buildCsv(count, modeDef, testingMode);
const absPath = path.resolve(outFile);
fs.writeFileSync(absPath, csv, 'utf8');

console.log(`Mode    : ${mode}${testingMode ? ' (TESTING: lastName=TESTER, NI prefixes: NN/PN/RA)' : ''}`);
console.log(`Records : ${count}`);
console.log(`Output  : ${absPath}`);
console.log(`Size    : ${(Buffer.byteLength(csv) / 1024).toFixed(1)} KB`);

