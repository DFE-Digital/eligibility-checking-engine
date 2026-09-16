-- =============================================================================
-- Camberwick establishment URN correction: 9002/9003 -> 600001/600002
-- =============================================================================
-- Background:
--   The Camberwick test establishments were originally seeded with placeholder
--   EstablishmentIDs (9002 = Camberwick Community School, 9003 = Camberwick
--   Academy). The DSI portal has since registered these organisations with
--   real URNs: 600001 (Camberwick Community School) and 600002 (Camberwick
--   Academy). This script repoints an already-seeded environment's data from
--   the old placeholder IDs to the correct URNs.
--
--   EstablishmentID is referenced by foreign keys from Applications and
--   MultiAcademyTrustEstablishments, so the old rows can't simply be UPDATEd
--   in place. Instead this script:
--     1. Inserts new Establishment rows under the correct URNs (copying all
--        other column data from the old rows).
--     2. Repoints Applications.EstablishmentId and
--        MultiAcademyTrustEstablishments.EstablishmentID to the new IDs.
--     3. Deletes the old placeholder Establishment rows.
--
-- Safe to re-run: every step is guarded by existence checks, so running this
-- again after it has already succeeded is a no-op.
--
-- Run the PREVIEW section first. Take a DB backup/snapshot before running in
-- dev/test/prod if you are unsure.
-- =============================================================================

USE EligibilityCheck;
GO

-- -----------------------------------------------------------------------------
-- 1. PREVIEW - run this first. No data is changed.
-- -----------------------------------------------------------------------------

SELECT EstablishmentID, EstablishmentName, LocalAuthorityID
FROM Establishments
WHERE EstablishmentID IN (9002, 9003, 600001, 600002);

SELECT COUNT(*) AS ApplicationsUsingOldIds
FROM Applications WHERE EstablishmentId IN (9002, 9003);

SELECT COUNT(*) AS MatLinksUsingOldIds
FROM MultiAcademyTrustEstablishments WHERE EstablishmentID IN (9002, 9003);
GO

-- -----------------------------------------------------------------------------
-- 2. MIGRATION
-- -----------------------------------------------------------------------------

BEGIN TRAN;

-- 2a. Insert the new establishment rows (only if the old placeholder still
--     exists and the new URN hasn't already been created)
IF EXISTS (SELECT 1 FROM Establishments WHERE EstablishmentID = 9002)
   AND NOT EXISTS (SELECT 1 FROM Establishments WHERE EstablishmentID = 600001)
BEGIN
    INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
    SELECT 600001, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta
    FROM Establishments WHERE EstablishmentID = 9002;
END

IF EXISTS (SELECT 1 FROM Establishments WHERE EstablishmentID = 9003)
   AND NOT EXISTS (SELECT 1 FROM Establishments WHERE EstablishmentID = 600002)
BEGIN
    INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
    SELECT 600002, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta
    FROM Establishments WHERE EstablishmentID = 9003;
END

-- 2b. Repoint FK references to the new IDs
UPDATE Applications SET EstablishmentId = 600001 WHERE EstablishmentId = 9002;
UPDATE Applications SET EstablishmentId = 600002 WHERE EstablishmentId = 9003;

UPDATE MultiAcademyTrustEstablishments SET EstablishmentID = 600001 WHERE EstablishmentID = 9002;
UPDATE MultiAcademyTrustEstablishments SET EstablishmentID = 600002 WHERE EstablishmentID = 9003;

-- 2c. Remove the old placeholder rows now nothing references them
DELETE FROM Establishments WHERE EstablishmentID IN (9002, 9003);

-- Review before committing
SELECT EstablishmentID, EstablishmentName, LocalAuthorityID
FROM Establishments WHERE LocalAuthorityID = 9000 ORDER BY EstablishmentID;

COMMIT TRAN;
-- ROLLBACK TRAN;
GO

-- -----------------------------------------------------------------------------
-- 3. POST-CHECK - should all return 0
-- -----------------------------------------------------------------------------

SELECT
    (SELECT COUNT(*) FROM Establishments WHERE EstablishmentID IN (9002, 9003))               AS OldEstablishmentRowsRemaining,
    (SELECT COUNT(*) FROM Applications WHERE EstablishmentId IN (9002, 9003))                  AS OldRefsInApplications,
    (SELECT COUNT(*) FROM MultiAcademyTrustEstablishments WHERE EstablishmentID IN (9002, 9003)) AS OldRefsInMAT;
GO
