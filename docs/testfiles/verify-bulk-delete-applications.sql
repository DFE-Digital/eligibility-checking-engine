-- SQL script to verify bulk delete operations
-- This script helps verify that applications were successfully deleted

-- Check if specific applications exist by GUID
SELECT 
    ApplicationID,
    Reference,
    ParentFirstName,
    ParentLastName,
    ChildFirstName,
    ChildLastName,
    Status,
    Created,
    Updated
FROM Applications 
WHERE ApplicationID IN (
    '12345678-1234-1234-1234-123456789abc',
    '87654321-4321-4321-4321-cba987654321',
    'abcdef01-2345-6789-abcd-ef0123456789',
    'fedcba09-8765-4321-0987-654321fedcba'
);

-- Count total applications before/after deletion
-- Run this before deletion to get baseline count
SELECT COUNT(*) as TotalApplications FROM Applications;

-- Check for orphaned application statuses (should be none after proper deletion)
SELECT 
    ApplicationStatusID,
    ApplicationID,
    Type,
    TimeStamp
FROM ApplicationStatuses 
WHERE ApplicationID IN (
    '12345678-1234-1234-1234-123456789abc',
    '87654321-4321-4321-4321-cba987654321',
    'abcdef01-2345-6789-abcd-ef0123456789',
    'fedcba09-8765-4321-0987-654321fedcba'
);

-- Check for orphaned application evidence (should be none after proper deletion)
SELECT 
    ApplicationEvidenceID,
    ApplicationID,
    EvidenceType,
    Created
FROM ApplicationEvidence 
WHERE ApplicationID IN (
    '12345678-1234-1234-1234-123456789abc',
    '87654321-4321-4321-4321-cba987654321',
    'abcdef01-2345-6789-abcd-ef0123456789',
    'fedcba09-8765-4321-0987-654321fedcba'
);

-- Get recent applications to verify new GUIDs for testing
SELECT TOP 10
    ApplicationID,
    Reference,
    ParentFirstName,
    ParentLastName,
    ChildFirstName,
    ChildLastName,
    Status,
    Created
FROM Applications 
ORDER BY Created DESC;

-- Count applications by status
SELECT 
    Status,
    COUNT(*) as Count
FROM Applications 
GROUP BY Status
ORDER BY Status;
