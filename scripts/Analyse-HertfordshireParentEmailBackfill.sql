-- =============================================================================
-- Hertfordshire (LocalAuthorityID 919) ParentEmail backfill - PHASE 1 ANALYSIS
-- =============================================================================
-- Background:
--   Hertfordshire FSM applications have been bulk-imported several times via
--   POST /application/bulk-import (ImportApplicationsUseCase). Every imported
--   row has EligibilityCheckHashID = NULL ("No hash for bulk import"), so the
--   import EVENTS are told apart by Created month, not by the hash column.
--
--   Observed on the read replica (Nov 2026):
--     Jul-Sep 2025 :  2,149 rows, 100% with ParentEmail   (early imports)
--     Nov 2025     : 23,408 rows, 100% with ParentEmail   ("last year's import")
--     Dec25-May26  :  1,138 rows, 100% with ParentEmail   (top-ups)
--     Jun 2026     : 23,698 rows,  87  with ParentEmail   (RE-CREATION, no email)
--     Sep 2026     :  6,034 rows,  62% with ParentEmail   (later import, partial)
--
--   The June 2026 re-creation was therefore ALSO a bulk import (not the bulk
--   check -> application path) and was loaded without emails. Hertfordshire
--   have asked us to copy the email from the earlier imported record onto the
--   equivalent live June record.
--
--   Note: ~13.7k of the Nov 2025 rows are still 'Receiving' (not archived), so
--   many parents currently have two live applications. Flag to the LA.
--
-- This script is READ-ONLY. It:
--   1. Materialises the two candidate sets into temp tables (ONE clustered
--      scan of dbo.Applications each - there is no index on LocalAuthorityID
--      or NINO, so we deliberately avoid repeated scans).
--   2. Profiles each set (counts, statuses, email quality, conflicts).
--   3. Runs a tiered match and reports how many live records can be fixed
--      with high confidence, how many are ambiguous, and how many are
--      unmatched.
--   4. Produces the candidate mapping that would feed Phase 2 (LA sign-off).
--
-- RUN ON THE READ REPLICA (profile 'Prod Read Replica', db 'read-replica').
-- Nothing here writes to user tables. Run it in ONE session (SSMS / Azure Data
-- Studio) so the #temp tables built in section 1 survive for sections 2-5.
-- Review each result set before moving on; the sections are independent once
-- the temp tables in section 1 exist.
-- =============================================================================

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
GO

-- Drop any temp tables left over from an earlier run IN THEIR OWN BATCH.
-- SQL Server compiles a whole batch before running it, so if an older #src /
-- #tgt (with a different column list) still exists in this session, later
-- statements would be validated against the stale shape and fail with
-- "Invalid column name" before the DROP below ever executes.
IF OBJECT_ID('tempdb..#src')        IS NOT NULL DROP TABLE #src;
IF OBJECT_ID('tempdb..#tgt')        IS NOT NULL DROP TABLE #tgt;
IF OBJECT_ID('tempdb..#match')      IS NOT NULL DROP TABLE #match;
IF OBJECT_ID('tempdb..#email_kind') IS NOT NULL DROP TABLE #email_kind;
IF OBJECT_ID('tempdb..#b_resolved') IS NOT NULL DROP TABLE #b_resolved;
GO

-- -----------------------------------------------------------------------------
-- 0. Parameters
-- -----------------------------------------------------------------------------
DECLARE @LocalAuthorityId   INT       = 919;
-- The six June CSVs (23,374 rows) landed 2026-06-25 15:59-16:11; a 237-row
-- top-up followed on 2026-06-26. The 88 rows loaded 4/10/22 June carry email
-- and EligibilityEndDate 2026-07-31, i.e. they are LAST year's cohort, so the
-- clean source/target boundary is 25 June, not 1 June.
-- SOURCE = every bulk import before the re-creation (all carry email).
DECLARE @SourceCreatedTo    DATETIME2 = '2026-06-25';
-- TARGET = the June 2026 re-creation import only. Widen @TargetCreatedTo to
-- '2026-10-01' if the LA confirms the Sep 2026 import should be fixed too.
DECLARE @TargetCreatedFrom  DATETIME2 = '2026-06-25';
DECLARE @TargetCreatedTo    DATETIME2 = '2026-07-01';

-- -----------------------------------------------------------------------------
-- 1. Build candidate sets (two scans, then everything else hits temp tables)
-- -----------------------------------------------------------------------------

-- SOURCE: bulk-imported (no hash) BEFORE the June re-creation, any status
-- (archived or still receiving - the email is valid either way).
SELECT
    a.ApplicationID,
    a.Reference,
    a.EstablishmentId,
    a.Status,
    a.Created,
    -- Mirror NinoValidation.Normalize: strip spaces/hyphens, upper-case
    UPPER(REPLACE(REPLACE(a.ParentNationalInsuranceNumber, ' ', ''), '-', ''))          AS Nino,
    UPPER(REPLACE(a.ParentNationalAsylumSeekerServiceNumber, ' ', ''))                  AS Nass,
    CAST(a.ParentDateOfBirth AS DATE)                                                  AS ParentDob,
    a.LocalAuthorityID,
    a.ParentFirstName,
    UPPER(a.ParentLastName)                                                            AS ParentLastName,
    UPPER(a.ChildFirstName)                                                            AS ChildFirstName,
    UPPER(a.ChildLastName)                                                             AS ChildLastName,
    CAST(a.ChildDateOfBirth AS DATE)                                                   AS ChildDob,
    NULLIF(LTRIM(RTRIM(a.ParentEmail)), '')                                            AS ParentEmail
INTO #src
FROM dbo.Applications a
WHERE a.LocalAuthorityID = @LocalAuthorityId
  AND a.EligibilityCheckHashID IS NULL
  AND a.Created < @SourceCreatedTo;

-- TARGET: live records from the June 2026 bulk-import re-creation.
-- Same hash-is-null signature as the source; separated purely by Created.
-- We keep rows that already have an email so we can report on them; the
-- backfill itself must only touch rows where ParentEmail IS NULL.
SELECT
    a.ApplicationID,
    a.Reference,
    a.EstablishmentId,
    a.Status,
    a.Created,
    UPPER(REPLACE(REPLACE(a.ParentNationalInsuranceNumber, ' ', ''), '-', ''))          AS Nino,
    UPPER(REPLACE(a.ParentNationalAsylumSeekerServiceNumber, ' ', ''))                  AS Nass,
    CAST(a.ParentDateOfBirth AS DATE)                                                  AS ParentDob,
    a.LocalAuthorityID,
    a.ParentFirstName,
    UPPER(a.ParentLastName)                                                            AS ParentLastName,
    UPPER(a.ChildFirstName)                                                            AS ChildFirstName,
    UPPER(a.ChildLastName)                                                             AS ChildLastName,
    CAST(a.ChildDateOfBirth AS DATE)                                                   AS ChildDob,
    NULLIF(LTRIM(RTRIM(a.ParentEmail)), '')                                            AS ParentEmail
INTO #tgt
FROM dbo.Applications a
WHERE a.LocalAuthorityID = @LocalAuthorityId
  AND a.EligibilityCheckHashID IS NULL      -- bulk-imported, not portal/bulk-check
  AND a.Status <> 'Archived'
  AND a.Created >= @TargetCreatedFrom
  AND a.Created <  @TargetCreatedTo;

CREATE CLUSTERED INDEX cx_src ON #src (Nino, ParentDob);
CREATE CLUSTERED INDEX cx_tgt ON #tgt (Nino, ParentDob);

SELECT 'src rows' AS Metric, COUNT(*) AS Value FROM #src
UNION ALL
SELECT 'tgt rows',           COUNT(*)          FROM #tgt;

-- -----------------------------------------------------------------------------
-- 2. Profile SOURCE (last year's import)
-- -----------------------------------------------------------------------------
-- 2a. By status
SELECT 'src by status' AS Section, Status, COUNT(*) AS Rows
FROM #src GROUP BY Status ORDER BY Rows DESC;

-- 2b. By month created (confirms the import window)
SELECT 'src by month' AS Section,
       FORMAT(Created, 'yyyy-MM') AS CreatedMonth, Status, COUNT(*) AS Rows
FROM #src GROUP BY FORMAT(Created, 'yyyy-MM'), Status ORDER BY CreatedMonth, Status;

-- 2c. Email quality
SELECT 'src email quality' AS Section,
       CASE
           WHEN ParentEmail IS NULL                   THEN 'null/blank'
           WHEN ParentEmail LIKE '%_@_%._%'           THEN 'plausible'
           ELSE                                            'junk'
       END AS EmailQuality,
       COUNT(*) AS Rows
FROM #src GROUP BY
       CASE
           WHEN ParentEmail IS NULL                   THEN 'null/blank'
           WHEN ParentEmail LIKE '%_@_%._%'           THEN 'plausible'
           ELSE                                            'junk'
       END;

-- 2d. Sample of "junk" values so we can decide on an exclusion rule
SELECT TOP 50 'src junk sample' AS Section, ParentEmail, COUNT(*) AS Rows
FROM #src
WHERE ParentEmail IS NOT NULL AND ParentEmail NOT LIKE '%_@_%._%'
GROUP BY ParentEmail ORDER BY Rows DESC;

-- 2e. Identifier coverage
SELECT 'src identifiers' AS Section,
       SUM(CASE WHEN Nino IS NOT NULL THEN 1 ELSE 0 END)                       AS WithNino,
       SUM(CASE WHEN Nino IS NULL AND Nass IS NOT NULL THEN 1 ELSE 0 END)      AS NassOnly,
       SUM(CASE WHEN Nino IS NULL AND Nass IS NULL THEN 1 ELSE 0 END)          AS Neither,
       COUNT(DISTINCT CONCAT(Nino, '|', ParentDob))                            AS DistinctParentsByNino
FROM #src;

-- 2f. CONFLICTS: same parent (Nino+DOB) archived with more than one email.
--     These cannot be auto-resolved by a NINO-only match.
SELECT 'src email conflicts' AS Section,
       COUNT(*) AS ParentsWithMultipleEmails,
       SUM(RowsAffected) AS SourceRowsInvolved
FROM (
    SELECT Nino, ParentDob, COUNT(*) AS RowsAffected
    FROM #src
    WHERE Nino IS NOT NULL AND ParentEmail LIKE '%_@_%._%'
    GROUP BY Nino, ParentDob
    HAVING COUNT(DISTINCT ParentEmail) > 1
) c;

-- -----------------------------------------------------------------------------
-- 3. Profile TARGET (this year's bulk-created live records)
-- -----------------------------------------------------------------------------
-- 3a. By status
SELECT 'tgt by status' AS Section, Status, COUNT(*) AS Rows
FROM #tgt GROUP BY Status ORDER BY Rows DESC;

-- 3b. By day created - should cluster tightly around the June bulk runs.
--     If it doesn't, tighten @TargetCreatedFrom/@TargetCreatedTo.
SELECT 'tgt by day' AS Section, CAST(Created AS DATE) AS CreatedDay, COUNT(*) AS Rows
FROM #tgt GROUP BY CAST(Created AS DATE) ORDER BY CreatedDay;

-- 3c. Email already present? (these must NOT be overwritten)
SELECT 'tgt email present' AS Section,
       SUM(CASE WHEN ParentEmail IS NULL THEN 1 ELSE 0 END) AS MissingEmail,
       SUM(CASE WHEN ParentEmail IS NOT NULL THEN 1 ELSE 0 END) AS HasEmail
FROM #tgt;

-- 3d. Identifier coverage
SELECT 'tgt identifiers' AS Section,
       SUM(CASE WHEN Nino IS NOT NULL THEN 1 ELSE 0 END)                       AS WithNino,
       SUM(CASE WHEN Nino IS NULL AND Nass IS NOT NULL THEN 1 ELSE 0 END)      AS NassOnly,
       COUNT(DISTINCT CONCAT(Nino, '|', ParentDob))                            AS DistinctParentsByNino
FROM #tgt;

-- 3e. Duplicate live applications: parents who have BOTH a live source row
--     (e.g. Nov 2025 still 'Receiving') AND a live June row. Not part of the
--     email fix, but the LA will want to know the scale.
SELECT 'live duplicates' AS Section,
       COUNT(DISTINCT t.ApplicationID) AS JuneRowsWithLiveOlderTwin
FROM #tgt t
JOIN #src s
  ON  s.Nino = t.Nino AND s.ParentDob = t.ParentDob
  AND s.ChildDob = t.ChildDob AND s.ChildLastName = t.ChildLastName
WHERE s.Status <> 'Archived' AND t.Nino IS NOT NULL;

-- -----------------------------------------------------------------------------
-- 4. Tiered match - one row per TARGET needing an email, with best tier
-- -----------------------------------------------------------------------------
-- Tier 1: Nino + ParentDob + ChildDob + ChildLastName   (same parent, same child)
-- Tier 2: Nino + ParentDob                              (same parent, sibling/other child)
-- Tier 3: Nass + ParentDob                              (NASS-only parents)
-- Within a tier, if the source candidates disagree on email the row is
-- flagged Ambiguous and the most recently Created source email is proposed
-- - but Ambiguous rows should go to the LA for adjudication, not be applied.
;WITH plausible_src AS (
    SELECT * FROM #src
    WHERE ParentEmail LIKE '%_@_%._%'
),
candidates AS (
    SELECT t.ApplicationID AS TargetApplicationID, 1 AS Tier,
           s.ApplicationID AS SourceApplicationID, s.Reference AS SourceReference,
           s.ParentEmail, s.Created AS SourceCreated
    FROM #tgt t
    JOIN plausible_src s
      ON  s.Nino = t.Nino AND s.ParentDob = t.ParentDob
      AND s.ChildDob = t.ChildDob AND s.ChildLastName = t.ChildLastName
    WHERE t.ParentEmail IS NULL AND t.Nino IS NOT NULL

    UNION ALL
    SELECT t.ApplicationID, 2, s.ApplicationID, s.Reference, s.ParentEmail, s.Created
    FROM #tgt t
    JOIN plausible_src s
      ON  s.Nino = t.Nino AND s.ParentDob = t.ParentDob
    WHERE t.ParentEmail IS NULL AND t.Nino IS NOT NULL

    UNION ALL
    SELECT t.ApplicationID, 3, s.ApplicationID, s.Reference, s.ParentEmail, s.Created
    FROM #tgt t
    JOIN plausible_src s
      ON  s.Nass = t.Nass AND s.ParentDob = t.ParentDob
    WHERE t.ParentEmail IS NULL AND t.Nino IS NULL AND t.Nass IS NOT NULL
),
best_tier AS (
    SELECT TargetApplicationID, MIN(Tier) AS Tier
    FROM candidates GROUP BY TargetApplicationID
),
best_candidates AS (
    SELECT c.*
    FROM candidates c
    JOIN best_tier b ON b.TargetApplicationID = c.TargetApplicationID AND b.Tier = c.Tier
),
email_counts AS (
    -- COUNT(DISTINCT) is not allowed with OVER, so aggregate separately
    SELECT TargetApplicationID, COUNT(DISTINCT ParentEmail) AS DistinctEmails
    FROM best_candidates GROUP BY TargetApplicationID
),
scored AS (
    SELECT bc.*, ec.DistinctEmails,
           ROW_NUMBER() OVER (PARTITION BY bc.TargetApplicationID
                              ORDER BY bc.SourceCreated DESC, bc.SourceApplicationID)     AS rn
    FROM best_candidates bc
    JOIN email_counts ec ON ec.TargetApplicationID = bc.TargetApplicationID
)
SELECT
    t.ApplicationID   AS TargetApplicationID,
    t.Reference       AS TargetReference,
    t.Status          AS TargetStatus,
    s.Tier,
    CASE WHEN s.DistinctEmails > 1 THEN 1 ELSE 0 END AS Ambiguous,
    s.ParentEmail     AS ProposedEmail,
    s.SourceApplicationID,
    s.SourceReference
INTO #match
FROM #tgt t
LEFT JOIN scored s ON s.TargetApplicationID = t.ApplicationID AND s.rn = 1
WHERE t.ParentEmail IS NULL;

-- 4a. Headline: how many live records can we fix, and how confidently?
SELECT 'match summary' AS Section,
       ISNULL(CAST(Tier AS VARCHAR(10)), 'unmatched') AS Tier,
       Ambiguous,
       COUNT(*) AS Targets
FROM #match
GROUP BY Tier, Ambiguous
ORDER BY Tier, Ambiguous;

-- 4b. Unmatched breakdown - why didn't they match?
--     (reason computed in a derived table: T-SQL does not allow subqueries in GROUP BY)
SELECT 'unmatched reasons' AS Section, r.Reason, COUNT(*) AS Targets
FROM (
    SELECT
        CASE
            WHEN t.Nino IS NULL AND t.Nass IS NULL THEN 'target has no NINO/NASS'
            WHEN NOT EXISTS (SELECT 1 FROM #src s WHERE s.Nino = t.Nino)
                                                    THEN 'NINO not in earlier imports'
            WHEN NOT EXISTS (SELECT 1 FROM #src s WHERE s.Nino = t.Nino AND s.ParentDob = t.ParentDob)
                                                    THEN 'NINO found but parent DOB differs'
            WHEN NOT EXISTS (SELECT 1 FROM #src s WHERE s.Nino = t.Nino AND s.ParentDob = t.ParentDob
                                                      AND s.ParentEmail LIKE '%_@_%._%')
                                                    THEN 'matched but source email null/junk'
            ELSE 'other'
        END AS Reason
    FROM #match m
    JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
    WHERE m.Tier IS NULL
) r
GROUP BY r.Reason
ORDER BY Targets DESC;

-- 4c. Ambiguous detail (masked) - these go to the LA to adjudicate
SELECT TOP 100 'ambiguous sample' AS Section,
       m.TargetReference, m.Tier,
       LEFT(m.ProposedEmail, 2) + '***' + SUBSTRING(m.ProposedEmail, CHARINDEX('@', m.ProposedEmail), 200) AS ProposedEmailMasked,
       m.SourceReference
FROM #match m
WHERE m.Ambiguous = 1
ORDER BY m.Tier, m.TargetReference;

-- 4d. Sanity: does any target match a source where the CHILD moved school?
--     (expected and harmless, but worth knowing the scale)
SELECT 'school changed' AS Section, COUNT(*) AS Targets
FROM #match m
JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
JOIN #src s ON s.ApplicationID = m.SourceApplicationID
WHERE m.Tier = 1 AND t.EstablishmentId <> s.EstablishmentId;

-- -----------------------------------------------------------------------------
-- 5. Phase 2 feed - three result sets for Hertfordshire to sign off
--    Export each to a tab of one workbook. Do NOT apply from here.
--    Deliberately excludes NINO and parent DOB: the LA can verify from
--    reference + names + child DOB + school, and it keeps the file low-risk.
-- -----------------------------------------------------------------------------
-- 5.0 Classify source emails. ~15% of last year's "parent" emails are in fact
--     school office addresses (admin@/office@, shared by many parents). The
--     LA needs to see this flag to decide whether to apply those or not.
IF OBJECT_ID('tempdb..#email_kind') IS NOT NULL DROP TABLE #email_kind;
SELECT ParentEmail,
       COUNT(DISTINCT Nino) AS ParentsUsing,
       CASE
           WHEN COUNT(DISTINCT Nino) >= 3                       THEN 'school/office (shared by 3+ parents)'
           WHEN MIN(ParentEmail) LIKE 'admin@%'
             OR MIN(ParentEmail) LIKE 'office@%'
             OR MIN(ParentEmail) LIKE 'head@%'
             OR MIN(ParentEmail) LIKE 'finance@%'
             OR MIN(ParentEmail) LIKE '%fsm%@%'                 THEN 'school/office (address pattern)'
           WHEN MIN(ParentEmail) LIKE '%.sch.uk'
             OR MIN(ParentEmail) LIKE '%academ%'                THEN 'school domain'
           ELSE 'parent'
       END AS EmailKind
INTO #email_kind
FROM #src
WHERE ParentEmail LIKE '%_@_%._%'
GROUP BY ParentEmail;

-- 5A. PROPOSED - unambiguous matches we intend to apply
SELECT
    t.Reference                                   AS ApplicationReference,
    t.LocalAuthorityID                            AS ApplicationLA,
    CONVERT(VARCHAR(10), t.Created, 103)          AS ApplicationCreated,
    YEAR(t.Created)                               AS ApplicationCreatedYear,
    t.ParentFirstName, t.ParentLastName           AS ParentSurname,
    t.ChildFirstName, t.ChildLastName             AS ChildSurname,
    CONVERT(VARCHAR(10), t.ChildDob, 103)         AS ChildDOB,
    t.EstablishmentId                             AS SchoolURN,
    t.Status                                      AS ApplicationStatus,
    m.ProposedEmail,
    ISNULL(k.EmailKind, 'parent')                 AS EmailType,
    CASE m.Tier WHEN 1 THEN 'same parent, same child'
                WHEN 2 THEN 'same parent, different child'
                WHEN 3 THEN 'NASS match' END       AS MatchBasis,
    m.SourceReference                             AS TakenFromReference,
    s.LocalAuthorityID                            AS TakenFromLA,
    CONVERT(VARCHAR(10), s.Created, 103)          AS TakenFromCreated,
    YEAR(s.Created)                               AS TakenFromCreatedYear,
    s.Status                                      AS TakenFromStatus
FROM #match m
JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
JOIN #src s ON s.ApplicationID = m.SourceApplicationID
LEFT JOIN #email_kind k ON k.ParentEmail = m.ProposedEmail
WHERE m.Tier IS NOT NULL AND m.Ambiguous = 0
ORDER BY t.EstablishmentId, t.ParentLastName, t.ChildLastName;

-- 5B. NEEDS A DECISION - parent found with 2+ different emails; one row per option
;WITH options AS (
    SELECT DISTINCT m.TargetApplicationID, s.ParentEmail, s.Reference AS SourceReference,
           s.LocalAuthorityID AS SourceLA, s.Created AS SourceCreated, s.Status AS SourceStatus
    FROM #match m
    JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
    JOIN #src s ON s.Nino = t.Nino AND s.ParentDob = t.ParentDob AND s.ParentEmail LIKE '%_@_%._%'
    WHERE m.Ambiguous = 1
)
SELECT
    t.Reference                                   AS ApplicationReference,
    t.LocalAuthorityID                            AS ApplicationLA,
    CONVERT(VARCHAR(10), t.Created, 103)          AS ApplicationCreated,
    YEAR(t.Created)                               AS ApplicationCreatedYear,
    t.ParentFirstName, t.ParentLastName           AS ParentSurname,
    t.ChildFirstName, t.ChildLastName             AS ChildSurname,
    CONVERT(VARCHAR(10), t.ChildDob, 103)         AS ChildDOB,
    t.EstablishmentId                             AS SchoolURN,
    o.ParentEmail                                 AS EmailOption,
    ISNULL(k.EmailKind, 'parent')                 AS EmailType,
    o.SourceReference                             AS SeenOnReference,
    o.SourceLA                                    AS SeenOnLA,
    CONVERT(VARCHAR(10), o.SourceCreated, 103)    AS SeenOnCreated,
    YEAR(o.SourceCreated)                         AS SeenOnCreatedYear,
    o.SourceStatus                                AS SeenOnStatus,
    CAST('' AS VARCHAR(10))                       AS UseThisOne   -- LA fills in Y
FROM options o
JOIN #tgt t ON t.ApplicationID = o.TargetApplicationID
LEFT JOIN #email_kind k ON k.ParentEmail = o.ParentEmail
ORDER BY t.Reference, o.SourceCreated DESC;

-- 5C. NO EMAIL AVAILABLE - nothing in earlier imports to copy from
SELECT
    t.Reference                                   AS ApplicationReference,
    t.LocalAuthorityID                            AS ApplicationLA,
    CONVERT(VARCHAR(10), t.Created, 103)          AS ApplicationCreated,
    YEAR(t.Created)                               AS ApplicationCreatedYear,
    t.ParentFirstName, t.ParentLastName           AS ParentSurname,
    t.ChildFirstName, t.ChildLastName             AS ChildSurname,
    CONVERT(VARCHAR(10), t.ChildDob, 103)         AS ChildDOB,
    t.EstablishmentId                             AS SchoolURN,
    t.Status                                      AS ApplicationStatus,
    CASE
        WHEN NOT EXISTS (SELECT 1 FROM #src s WHERE s.Nino = t.Nino)
             THEN 'parent not in any earlier import'
        WHEN NOT EXISTS (SELECT 1 FROM #src s WHERE s.Nino = t.Nino AND s.ParentDob = t.ParentDob)
             THEN 'parent found but date of birth differs'
        ELSE 'earlier record had no usable email'
    END                                           AS Reason,
    CAST('' AS VARCHAR(200))                      AS EmailIfKnown  -- LA may supply
FROM #match m
JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
WHERE m.Tier IS NULL
ORDER BY t.EstablishmentId, t.ParentLastName;

-- 5E. TAB B RESOLVED - apply the rule Hertfordshire agreed (06/11/2026):
--     "each of this year's applications takes the email from its own earlier
--      record". Implemented as a ranked preference per application:
--        1. earlier record for the SAME CHILD at the SAME SCHOOL
--        2. earlier record for the same child at a different school (moved)
--        3. earlier record for the same parent only (sibling)
--      and within the winning level:
--        a. a genuine parent email beats a school office address
--           (otherwise ~30 rows would get admin@school purely because it was
--            imported a few weeks later than the parent's own address)
--        b. then the MOST RECENTLY created record.
--     RuleApplied says which level won and how any tie was broken;
--     OtherEmailsSeen lists what was rejected so the LA can eyeball it.
IF OBJECT_ID('tempdb..#b_resolved') IS NOT NULL DROP TABLE #b_resolved;
;WITH cand AS (
    SELECT m.TargetApplicationID,
           s.ApplicationID   AS SourceApplicationID,
           s.Reference       AS SourceReference,
           s.ParentEmail,
           s.Created         AS SourceCreated,
           s.Status          AS SourceStatus,
           s.LocalAuthorityID AS SourceLA,
           CASE WHEN ISNULL(k.EmailKind, 'parent') = 'parent' THEN 1 ELSE 0 END                   AS IsParentEmail,
           CASE WHEN s.ChildDob = t.ChildDob AND s.ChildLastName = t.ChildLastName THEN 1 ELSE 0 END AS SameChild,
           CASE WHEN s.EstablishmentId = t.EstablishmentId THEN 1 ELSE 0 END                        AS SameSchool
    FROM #match m
    JOIN #tgt t ON t.ApplicationID = m.TargetApplicationID
    JOIN #src s ON s.Nino = t.Nino AND s.ParentDob = t.ParentDob
               AND s.ParentEmail LIKE '%_@_%._%'
    LEFT JOIN #email_kind k ON k.ParentEmail = s.ParentEmail
    WHERE m.Ambiguous = 1
),
grp AS (
    SELECT TargetApplicationID, SameChild, SameSchool,
           COUNT(DISTINCT ParentEmail)                                       AS EmailsInGroup,
           COUNT(DISTINCT CASE WHEN IsParentEmail = 1 THEN ParentEmail END)  AS ParentEmailsInGroup,
           COUNT(DISTINCT CASE WHEN IsParentEmail = 0 THEN ParentEmail END)  AS OfficeEmailsInGroup
    FROM cand GROUP BY TargetApplicationID, SameChild, SameSchool
),
ranked AS (
    SELECT c.*, g.EmailsInGroup, g.ParentEmailsInGroup, g.OfficeEmailsInGroup,
           ROW_NUMBER() OVER (PARTITION BY c.TargetApplicationID
                              ORDER BY c.SameChild DESC, c.SameSchool DESC,
                                       c.IsParentEmail DESC, c.SourceCreated DESC, c.SourceApplicationID) AS rn
    FROM cand c
    JOIN grp g ON g.TargetApplicationID = c.TargetApplicationID AND g.SameChild = c.SameChild AND g.SameSchool = c.SameSchool
),
chosen AS (SELECT * FROM ranked WHERE rn = 1),
others AS (
    SELECT x.TargetApplicationID, STRING_AGG(x.ParentEmail, '; ') AS OtherEmailsSeen
    FROM (SELECT DISTINCT c.TargetApplicationID, c.ParentEmail FROM cand c) x
    JOIN chosen ch ON ch.TargetApplicationID = x.TargetApplicationID AND ch.ParentEmail <> x.ParentEmail
    GROUP BY x.TargetApplicationID
)
SELECT
    t.Reference                                   AS ApplicationReference,
    t.LocalAuthorityID                            AS ApplicationLA,
    CONVERT(VARCHAR(10), t.Created, 103)          AS ApplicationCreated,
    YEAR(t.Created)                               AS ApplicationCreatedYear,
    t.ParentFirstName, t.ParentLastName           AS ParentSurname,
    t.ChildFirstName, t.ChildLastName             AS ChildSurname,
    CONVERT(VARCHAR(10), t.ChildDob, 103)         AS ChildDOB,
    t.EstablishmentId                             AS SchoolURN,
    t.Status                                      AS ApplicationStatus,
    ch.ParentEmail                                AS ChosenEmail,
    ISNULL(k.EmailKind, 'parent')                 AS EmailType,
    CASE WHEN ch.SameChild = 1 AND ch.SameSchool = 1 THEN 'same child, same school'
         WHEN ch.SameChild = 1                       THEN 'same child, moved school'
         ELSE                                              'same parent only (sibling)' END
    + CASE
          WHEN ch.EmailsInGroup = 1 THEN ''
          WHEN ch.ParentEmailsInGroup >= 1 AND ch.OfficeEmailsInGroup >= 1
               THEN ' - parent email preferred over school office'
                    + CASE WHEN ch.ParentEmailsInGroup > 1
                           THEN ', most recent of ' + CAST(ch.ParentEmailsInGroup AS VARCHAR(5)) ELSE '' END
          ELSE ' - most recent of ' + CAST(ch.EmailsInGroup AS VARCHAR(5))
      END                                         AS RuleApplied,
    o.OtherEmailsSeen,
    ch.SourceReference                            AS TakenFromReference,
    ch.SourceLA                                   AS TakenFromLA,
    CONVERT(VARCHAR(10), ch.SourceCreated, 103)   AS TakenFromCreated,
    YEAR(ch.SourceCreated)                        AS TakenFromCreatedYear,
    ch.SourceStatus                               AS TakenFromStatus
INTO #b_resolved
FROM chosen ch
JOIN #tgt t ON t.ApplicationID = ch.TargetApplicationID
LEFT JOIN #email_kind k ON k.ParentEmail = ch.ParentEmail
LEFT JOIN others o ON o.TargetApplicationID = ch.TargetApplicationID;

SELECT * FROM #b_resolved
ORDER BY RuleApplied, SchoolURN, ParentSurname;

-- 5D. Cover-note numbers
SELECT 'blank emails in June cohort'            AS Metric, CAST(COUNT(*) AS VARCHAR(20)) AS Value FROM #match
UNION ALL SELECT 'proposed (tab A)',                       CAST(COUNT(*) AS VARCHAR(20)) FROM #match WHERE Tier IS NOT NULL AND Ambiguous = 0
UNION ALL SELECT '  of which school/office address',       CAST(COUNT(*) AS VARCHAR(20)) FROM #match m JOIN #email_kind k ON k.ParentEmail = m.ProposedEmail
                                                                   WHERE m.Tier IS NOT NULL AND m.Ambiguous = 0 AND k.EmailKind <> 'parent'
UNION ALL SELECT 'needs a decision (tab B)',               CAST(COUNT(*) AS VARCHAR(20)) FROM #match WHERE Ambiguous = 1
UNION ALL SELECT '  resolved by rule (tab B resolved)',    CAST(COUNT(*) AS VARCHAR(20)) FROM #b_resolved
UNION ALL SELECT 'no email available (tab C)',             CAST(COUNT(*) AS VARCHAR(20)) FROM #match WHERE Tier IS NULL
UNION ALL SELECT 'B rule: ' + RuleApplied,                 CAST(COUNT(*) AS VARCHAR(20)) FROM #b_resolved GROUP BY RuleApplied;

-- Clean-up (optional; temp tables die with the session)
-- DROP TABLE #b_resolved; DROP TABLE #email_kind; DROP TABLE #match; DROP TABLE #tgt; DROP TABLE #src;
