-- Returns BulkCheckIDs + SubmittedDate for a given LocalAuthorityID as batches of 100.
-- Each row is a comma-separated string of "id|date" pairs ordered by SubmittedDate DESC.
-- Copy each row value into the $bulkCheckBatches array in Check-BulkCheckProgress.ps1.

DECLARE @LocalAuthorityID INT = 330   -- <-- change this

SELECT STRING_AGG(
    CAST(BulkCheckID AS NVARCHAR(MAX)) + '|' + CONVERT(NVARCHAR(23), SubmittedDate, 126),
    ','
) WITHIN GROUP (ORDER BY SubmittedDate DESC) AS BulkChecks
FROM (
    SELECT
        BulkCheckID,
        SubmittedDate,
        CEILING(ROW_NUMBER() OVER (ORDER BY SubmittedDate DESC) * 1.0 / 100) AS BatchNum
    FROM [dbo].[BulkChecks]
    WHERE LocalAuthorityID = @LocalAuthorityID
    -- Optional: restrict to a date range (e.g. last week)
    -- AND SubmittedDate >= DATEADD(day, -7, GETUTCDATE())
) t
GROUP BY BatchNum
ORDER BY BatchNum
