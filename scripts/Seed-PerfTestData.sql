-- =============================================================================
-- Seed-PerfTestData.sql
-- LOCAL DEVELOPMENT ONLY — do NOT run against dev/staging/production.
--
-- Creates 20 BulkChecks with 5,000 EligibilityChecks each (100,000 total)
-- for LocalAuthorityID = 201, all submitted within the last 7 days.
-- All rows tagged BulkCheckID LIKE 'perf-test-%' for easy cleanup.
--
-- PURPOSE: Reproduce the GetBulkStatuses timeout bug locally.
-- Each batch is capped at 5,000 rows (the real application limit).
-- 20 batches × 5,000 = 100,000 rows, spaced 8 hours apart so all
-- 20 batches fall within the 7-day query window (20 × 8h = 160h < 168h).
--
-- CLEANUP: Run the DELETE block at the bottom when done.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @LAID        INT = 201;
DECLARE @BatchCount  INT = 21;      -- set to 20 for full perf test
DECLARE @PerBatch    INT = 5000;      -- set to 5000 for full perf test

-- ---------------------------------------------------------------------------
-- Cleanup any previous run
-- ---------------------------------------------------------------------------
PRINT 'Cleaning up previous perf-test data...';
DELETE FROM [dbo].[EligibilityCheck] WHERE BulkCheckID LIKE 'perf-test-%';
DELETE FROM [dbo].[BulkChecks]       WHERE BulkCheckID LIKE 'perf-test-%';

-- ---------------------------------------------------------------------------
-- Insert BulkChecks spread across the last 6 days
-- ---------------------------------------------------------------------------
PRINT 'Inserting BulkChecks...';

DECLARE @i INT = 1;
WHILE @i <= @BatchCount
BEGIN
    INSERT INTO [dbo].[BulkChecks]
        (BulkCheckID, Filename, EligibilityType, SubmittedDate, SubmittedBy,
         LocalAuthorityID, FinalNameInCheck, NumberOfRecords)
    VALUES (
        'perf-test-' + CAST(@i AS VARCHAR(10)),
        'perf-test-batch-' + CAST(@i AS VARCHAR(10)) + '.csv',
        'FreeSchoolMeals',
        DATEADD(HOUR, -(@i * 8), GETUTCDATE()),  -- 8h spacing keeps 20 batches within 7 days
        'perf-test@dfe.gov.uk',
        @LAID,
        'perf-test-batch-' + CAST(@i AS VARCHAR(10)) + '.csv',
        @PerBatch
    );
    SET @i = @i + 1;
END

PRINT 'BulkChecks inserted: ' + CAST(@BatchCount AS VARCHAR);

-- ---------------------------------------------------------------------------
-- Insert EligibilityCheck rows via tally-table (no per-row CURSOR)
-- ---------------------------------------------------------------------------
PRINT 'Inserting EligibilityCheck rows (this may take a few seconds)...';

DECLARE @b INT = 1;
DECLARE @bcid VARCHAR(50);

WHILE @b <= @BatchCount
BEGIN
    SET @bcid = 'perf-test-' + CAST(@b AS VARCHAR(10));

    ;WITH
        N1  AS (SELECT 1 n UNION ALL SELECT 1),
        N2  AS (SELECT 1 n FROM N1  a, N1  b),
        N4  AS (SELECT 1 n FROM N2  a, N2  b),
        N8  AS (SELECT 1 n FROM N4  a, N4  b),
        N16 AS (SELECT 1 n FROM N8  a, N8  b),   -- 65,536 rows — enough for 25,000
        Tally AS (SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS rn FROM N16)
    INSERT INTO [dbo].[EligibilityCheck]
        (EligibilityCheckID, [Type], [Status], Created, Updated,
         CheckData, Source, UserName, OrganisationID, OrganisationType,
         IsDeleted, BulkCheckID)
    SELECT TOP (@PerBatch)
        'perf-' + @bcid + '-' + CAST(rn AS VARCHAR(10)),
        'FreeSchoolMeals',
        CASE (rn % 3)
            WHEN 0 THEN 'eligible'
            WHEN 1 THEN 'notEligible'
            ELSE        'parentNotFound'
        END,
        DATEADD(HOUR, -(@b * 8), GETUTCDATE()),
        DATEADD(HOUR, -(@b * 8), GETUTCDATE()),
        '{"LastName":"TESTER","DateOfBirth":"1990-01-01","NationalInsuranceNumber":"NN000001C"}',
        'bulk',
        'perf-test@dfe.gov.uk',
        @LAID,
        'local-authority',
        0,
        @bcid
    FROM Tally;

    PRINT '  Batch ' + CAST(@b AS VARCHAR) + ' done (' + @bcid + ')';
    SET @b = @b + 1;
END

-- ---------------------------------------------------------------------------
-- Summary
-- ---------------------------------------------------------------------------
PRINT '';
PRINT '=== Seed complete ===';

SELECT
    b.BulkCheckID,
    b.SubmittedDate,
    COUNT(e.EligibilityCheckID) AS CheckCount
FROM [dbo].[BulkChecks] b
LEFT JOIN [dbo].[EligibilityCheck] e ON e.BulkCheckID = b.BulkCheckID
WHERE b.BulkCheckID LIKE 'perf-test-%'
GROUP BY b.BulkCheckID, b.SubmittedDate
ORDER BY b.SubmittedDate DESC;

-- ---------------------------------------------------------------------------
-- CLEANUP — run this block to remove all perf-test rows
-- ---------------------------------------------------------------------------
-- DELETE FROM [dbo].[EligibilityCheck] WHERE BulkCheckID LIKE 'perf-test-%';
-- DELETE FROM [dbo].[BulkChecks]       WHERE BulkCheckID LIKE 'perf-test-%';
