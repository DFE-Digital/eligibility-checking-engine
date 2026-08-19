-- =============================================================================
-- Seed-PerfTestData.sql
-- Creates 10 BulkChecks with 10,000 EligibilityChecks each (100,000 total)
-- for LocalAuthorityID = 201, all submitted within the last 7 days.
-- All rows are tagged with BulkCheckID LIKE 'perf-test-%' for easy cleanup.
--
-- Run against: EligibilityCheck (local DB)
-- Cleanup:     Run the DELETE block at the bottom
-- =============================================================================

SET NOCOUNT ON;

DECLARE @LAID        INT = 201;
DECLARE @BatchCount  INT = 10;
DECLARE @PerBatch    INT = 25000;   -- 10 x 25,000 = 250,000 EligibilityCheck rows

-- ---------------------------------------------------------------------------
-- Cleanup any previous run
-- ---------------------------------------------------------------------------
PRINT 'Cleaning up previous perf-test data...';
DELETE FROM [dbo].[EligibilityCheck] WHERE BulkCheckID LIKE 'perf-test-%';
DELETE FROM [dbo].[BulkChecks]       WHERE BulkCheckID LIKE 'perf-test-%';

-- ---------------------------------------------------------------------------
-- Insert BulkChecks (one per "batch"), spread across the last 6 days
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
        DATEADD(HOUR, -(@i * 12), GETUTCDATE()),   -- spread: 12h, 24h, 36h ... ago
        'perf-test@dfe.gov.uk',
        @LAID,
        'perf-test-batch-' + CAST(@i AS VARCHAR(10)) + '.csv',
        @PerBatch
    );
    SET @i = @i + 1;
END

PRINT 'BulkChecks inserted: ' + CAST(@BatchCount AS VARCHAR);

-- ---------------------------------------------------------------------------
-- Insert EligibilityCheck rows via a tally-table approach (no CURSOR per row)
-- Statuses are spread across eligible / notEligible / parentNotFound to
-- simulate a realistic completed batch (no queuedForProcessing rows, so the
-- gateway will mark all batches as Completed).
-- ---------------------------------------------------------------------------
PRINT 'Inserting EligibilityCheck rows (this may take a few seconds)...';

DECLARE @b INT = 1;
DECLARE @bcid VARCHAR(50);

WHILE @b <= @BatchCount
BEGIN
    SET @bcid = 'perf-test-' + CAST(@b AS VARCHAR(10));

    -- Tally CTE generates enough rows; TOP caps it at @PerBatch
    ;WITH
        N1  AS (SELECT 1 n UNION ALL SELECT 1),
        N2  AS (SELECT 1 n FROM N1  a, N1  b),
        N4  AS (SELECT 1 n FROM N2  a, N2  b),
        N8  AS (SELECT 1 n FROM N4  a, N4  b),
        N16 AS (SELECT 1 n FROM N8  a, N8  b),   -- 65,536 rows — enough for 10,000
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
        DATEADD(HOUR, -(@b * 12), GETUTCDATE()),
        DATEADD(HOUR, -(@b * 12), GETUTCDATE()),
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
-- CLEANUP (run separately when done testing)
-- ---------------------------------------------------------------------------
-- DELETE FROM [dbo].[EligibilityCheck] WHERE BulkCheckID LIKE 'perf-test-%';
-- DELETE FROM [dbo].[BulkChecks]       WHERE BulkCheckID LIKE 'perf-test-%';
