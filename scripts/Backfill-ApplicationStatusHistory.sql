-- =============================================================================
-- ELIG-2617 backfill: create missing ApplicationStatuses history rows
-- =============================================================================
-- Background:
--   Before the ELIG-2617 fix, applications created via bulk import
--   (ApplicationGateway.BulkImportApplications) were never given an initial
--   row in ApplicationStatuses. This script backfills one synthetic history
--   row for every existing Application that currently has ZERO rows in
--   ApplicationStatuses, using that application's CURRENT Status value and
--   its Created timestamp - i.e. what AddStatusHistory would have written
--   at creation time had the bug not existed.
--
-- IMPORTANT CAVEATS:
--   1. This is a best-effort reconstruction, not a guarantee of the true
--      original status. For the vast majority of bulk-imported rows the
--      current Status IS the original (unchanged) status, so this is safe.
--      If an affected application's status has since been changed via the
--      normal update endpoints, intermediate history would still be missing
--      (only this single backfilled entry + any updates made after the fix
--      was deployed will exist).
--   2. If an affected application's CURRENT status is already 'Archived'
--      (i.e. it was bulk-imported directly as Archived, or archived before
--      this backfill ran, with no other history), backfilling a single
--      'Archived' entry does NOT make it restorable - there is no way to
--      recover a "previous" status that was never recorded. Those rows are
--      flagged separately below for manual review; the restore endpoint will
--      now return a clear 400 error for them (post ELIG-2617 defensive
--      check) instead of throwing an unhandled exception.
--   3. Idempotent / safe to re-run: only inserts for ApplicationIDs that
--      still have zero rows in ApplicationStatuses.
--   4. Run the PREVIEW section first and review row counts/samples before
--      running the INSERT section. Take a DB backup/snapshot before running
--      in dev/test/prod if you are unsure.
-- =============================================================================

USE EligibilityCheck;
GO

-- -----------------------------------------------------------------------------
-- 1. PREVIEW - run this first. No data is changed.
-- -----------------------------------------------------------------------------

-- Overall counts
SELECT
    COUNT(*)                                                                AS TotalApplications,
    SUM(CASE WHEN s.ApplicationID IS NULL THEN 1 ELSE 0 END)                AS ApplicationsWithNoHistory,
    SUM(CASE WHEN s.ApplicationID IS NULL AND a.Status = 'Archived'
             THEN 1 ELSE 0 END)                                             AS ArchivedWithNoHistory_CannotBeFullyFixed
FROM Applications a
LEFT JOIN (SELECT DISTINCT ApplicationID FROM ApplicationStatuses) s
    ON s.ApplicationID = a.ApplicationID;
GO

-- Sample of the specific rows that will receive a backfilled history entry
SELECT
    a.ApplicationID,
    a.Reference,
    a.Status,
    a.Created,
    a.LocalAuthorityID
FROM Applications a
LEFT JOIN (SELECT DISTINCT ApplicationID FROM ApplicationStatuses) s
    ON s.ApplicationID = a.ApplicationID
WHERE s.ApplicationID IS NULL
ORDER BY a.Created;
GO

-- -----------------------------------------------------------------------------
-- 2. BACKFILL - inserts one history row per orphaned application.
--    Wrapped in a transaction: verify the SELECT COUNT(*) output below before
--    trusting the COMMIT, or change COMMIT TRAN to ROLLBACK TRAN to do a
--    dry run against a real transaction.
-- -----------------------------------------------------------------------------

BEGIN TRAN;

INSERT INTO ApplicationStatuses (ApplicationStatusID, ApplicationID, Type, TimeStamp)
SELECT
    LOWER(CONVERT(varchar(36), NEWID())) AS ApplicationStatusID,
    a.ApplicationID,
    a.Status                              AS Type,
    a.Created                             AS TimeStamp
FROM Applications a
LEFT JOIN (SELECT DISTINCT ApplicationID FROM ApplicationStatuses) s
    ON s.ApplicationID = a.ApplicationID
WHERE s.ApplicationID IS NULL
  AND a.Status IS NOT NULL;

-- Review this before committing
SELECT @@ROWCOUNT AS RowsInserted;

-- Flip to ROLLBACK TRAN while testing, COMMIT TRAN once you're happy
COMMIT TRAN;
-- ROLLBACK TRAN;
GO

-- -----------------------------------------------------------------------------
-- 3. POST-CHECK - should return 0 rows (bar any still-Archived-with-no-prior-
--    history applications noted in the caveats above, which by definition
--    cannot be fixed by this script).
-- -----------------------------------------------------------------------------

SELECT
    COUNT(*) AS ApplicationsStillWithNoHistory
FROM Applications a
LEFT JOIN (SELECT DISTINCT ApplicationID FROM ApplicationStatuses) s
    ON s.ApplicationID = a.ApplicationID
WHERE s.ApplicationID IS NULL;
GO
