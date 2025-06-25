-- Bulk Import Verification Script
-- Run this after executing the bulk import via Postman (supports both CSV and JSON formats)

-- 1. Count applications before and after
PRINT 'Total Applications Count:'
SELECT COUNT(*) AS TotalApplications FROM [dbo].[Applications]

-- 2. Check recent imports (last 10 minutes)
PRINT 'Recent Imports (last 10 minutes):'
SELECT [ApplicationID], [Type], [Reference], [LocalAuthorityId], [EstablishmentId],
       [ParentFirstName], [ParentLastName], [ParentEmail],
       [ChildFirstName], [ChildLastName], [EligibilityCheckHashID], 
       [Created], [Status]
FROM [dbo].[Applications]
WHERE [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())
ORDER BY [Created] DESC

-- 3. Verify NULL EligibilityCheckHashID for bulk imports
PRINT 'Bulk Imports with NULL Hash (requirement verification):'
SELECT COUNT(*) AS BulkImportCount
FROM [dbo].[Applications]
WHERE [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())
  AND [EligibilityCheckHashID] IS NULL

-- 4. Check specific test data was imported (from CSV or JSON)
PRINT 'Verify specific names from import file:'
SELECT [ApplicationID], [ParentFirstName], [ParentLastName], [ParentEmail],
       [ChildFirstName], [ChildLastName], [EstablishmentId], [Created]
FROM [dbo].[Applications]
WHERE [ParentFirstName] IN ('John', 'Sarah', 'Michael', 'Lisa', 'David')
  AND [ParentLastName] IN ('Smith', 'Johnson', 'Brown', 'Davis', 'Wilson')
  AND [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())
ORDER BY [Created] DESC

-- 5. Check establishment mappings worked correctly
PRINT 'Verify establishment mappings:'
SELECT a.[ParentFirstName], a.[ParentLastName], a.[EstablishmentId], e.[EstablishmentName]
FROM [dbo].[Applications] a
LEFT JOIN [dbo].[Establishments] e ON a.[EstablishmentId] = e.[EstablishmentId]
WHERE a.[Created] >= DATEADD(MINUTE, -10, GETUTCDATE())
ORDER BY a.[Created] DESC

-- 6. Check for any failed imports (if any records are missing)
PRINT 'Expected 5 records from import file. Actual imported:'
SELECT COUNT(*) AS ActualImported
FROM [dbo].[Applications]
WHERE [ParentFirstName] IN ('John', 'Sarah', 'Michael', 'Lisa', 'David')
  AND [ParentLastName] IN ('Smith', 'Johnson', 'Brown', 'Davis', 'Wilson')
  AND [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())

-- ============================================================================
-- CLEANUP SECTION - Run these queries to delete test entries after verification
-- ============================================================================

-- 7. Delete test entries (UNCOMMENT TO RUN)
-- WARNING: This will permanently delete the test import data!

/*
-- First, show what will be deleted
PRINT 'Test entries to be deleted:'
SELECT [ApplicationID], [ParentFirstName], [ParentLastName], [ParentEmail],
       [ChildFirstName], [ChildLastName], [Created]
FROM [dbo].[Applications]
WHERE [ParentFirstName] IN ('John', 'Sarah', 'Michael', 'Lisa', 'David')
  AND [ParentLastName] IN ('Smith', 'Johnson', 'Brown', 'Davis', 'Wilson')
  AND [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())

-- Delete the test entries
DELETE FROM [dbo].[Applications]
WHERE [ParentFirstName] IN ('John', 'Sarah', 'Michael', 'Lisa', 'David')
  AND [ParentLastName] IN ('Smith', 'Johnson', 'Brown', 'Davis', 'Wilson')
  AND [Created] >= DATEADD(MINUTE, -10, GETUTCDATE())

PRINT 'Test entries deleted.'
*/

-- Alternative: Delete by specific criteria if you want more precision
/*
-- Delete specific test entries by exact match (safer approach)
DELETE FROM [dbo].[Applications]
WHERE ([ParentFirstName] = 'John' AND [ParentLastName] = 'Smith' AND [ParentEmail] = 'john.smith@example.com')
   OR ([ParentFirstName] = 'Sarah' AND [ParentLastName] = 'Johnson' AND [ParentEmail] = 'sarah.johnson@example.com')
   OR ([ParentFirstName] = 'Michael' AND [ParentLastName] = 'Brown' AND [ParentEmail] = 'michael.brown@example.com')
   OR ([ParentFirstName] = 'Lisa' AND [ParentLastName] = 'Davis' AND [ParentEmail] = 'lisa.davis@example.com')
   OR ([ParentFirstName] = 'David' AND [ParentLastName] = 'Wilson' AND [ParentEmail] = 'david.wilson@example.com')
*/

-- Verify deletion (run after deleting)
/*
PRINT 'Verification after deletion - should return 0:'
SELECT COUNT(*) AS RemainingTestEntries
FROM [dbo].[Applications]
WHERE [ParentFirstName] IN ('John', 'Sarah', 'Michael', 'Lisa', 'David')
  AND [ParentLastName] IN ('Smith', 'Johnson', 'Brown', 'Davis', 'Wilson')
  AND [ParentEmail] IN ('john.smith@example.com', 'sarah.johnson@example.com', 
                        'michael.brown@example.com', 'lisa.davis@example.com', 
                        'david.wilson@example.com')
*/
