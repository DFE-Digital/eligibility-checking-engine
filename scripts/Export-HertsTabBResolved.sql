-- =============================================================================
-- Export-HertsTabBResolved.sql  (generated from Analyse-HertfordshireParentEmailBackfill.sql)
-- Runs ONLY what is needed to produce the "Tab B resolved" sheet for Hertfordshire.
-- Connect to: Prod Read Replica (read-replica). Run the WHOLE file. One result grid
-- (~249 rows as of 11 Nov; shrinks as Herts archive) -> Save as Excel -> Herts-email-backfill-B-resolved.xlsx (OneDrive, not repo).
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

