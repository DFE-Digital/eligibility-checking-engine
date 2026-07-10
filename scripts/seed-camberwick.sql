-- =============================================================================
-- Camberwick FSM Expansion Test Organisations
-- =============================================================================
-- These organisations are reserved for FSM expansion testing only.
-- They must not be used by external or production users.
-- Suitable for DEV / TEST / PREPROD environments.
--
-- Summary:
--   LA  (FSM basic version)    : Camberwick Council            (LocalAuthorityID = 9000)
--   MAT (FSM enhanced version) : Camberwick Academy Trust      (MultiAcademyTrustID = 9001)
--   School (FSM enhanced)      : Camberwick Community School   (EstablishmentID = 600001, DSI URN 600001)
--   Academy (FSM enhanced)     : Camberwick Academy            (EstablishmentID = 600002, DSI URN 600002)
--
-- NOTE: EstablishmentIDs were updated from the original placeholders (9002/9003)
-- to match the real URNs registered in the DSI portal (600001/600002) - see
-- scripts/Backfill-CamberwickEstablishmentUrns.sql for the one-off migration
-- that repointed existing data in already-seeded environments.
-- =============================================================================

USE EligibilityCheck;
GO

-- 1. Local Authority
INSERT INTO LocalAuthorities (LocalAuthorityID, LaName, SchoolCanReviewEvidence)
VALUES
    (9000, 'Camberwick Council', 0);
GO

-- 2. Establishments
--    600001 Camberwick Community School : parent = LA only  (LocalAuthorityID = 9000)
--    600002 Camberwick Academy          : parent = LA + MAT (LocalAuthorityID = 9000, MultiAcademyTrustID = 9001)
INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
VALUES
    (600001, 'Camberwick Community School', 'CW1 2BB', '1 School Lane',  '', 'Camberwick', '', 1, 9000, 'Community School',  0),
    (600002, 'Camberwick Academy',          'CW1 3CC', '2 Academy Road', '', 'Camberwick', '', 1, 9000, 'Academy Converter', 0);
GO

-- 3. Multi-Academy Trust
INSERT INTO MultiAcademyTrusts (MultiAcademyTrustID, Name)
VALUES
    (9001, 'Camberwick Academy Trust');
GO

-- 4. Link the academy to the MAT (school is LA-only, so no entry for 600001)
INSERT INTO MultiAcademyTrustEstablishments (MultiAcademyTrustID, EstablishmentID)
VALUES
    (9001, 600002);
GO
