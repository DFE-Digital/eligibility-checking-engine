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
--   School (FSM enhanced)      : Camberwick Community School   (EstablishmentID = 9002)
--   Academy (FSM enhanced)     : Camberwick Academy            (EstablishmentID = 9003)
-- =============================================================================

USE EligibilityCheck;
GO

-- 1. Local Authority
INSERT INTO LocalAuthorities (LocalAuthorityID, LaName, SchoolCanReviewEvidence)
VALUES
    (9000, 'Camberwick Council', 0);
GO

-- 2. Establishments
--    9002 Camberwick Community School : parent = LA only  (LocalAuthorityID = 9000)
--    9003 Camberwick Academy          : parent = LA + MAT (LocalAuthorityID = 9000, MultiAcademyTrustID = 9001)
INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
VALUES
    (9002, 'Camberwick Community School', 'CW1 2BB', '1 School Lane',  '', 'Camberwick', '', 1, 9000, 'Community School',  0),
    (9003, 'Camberwick Academy',          'CW1 3CC', '2 Academy Road', '', 'Camberwick', '', 1, 9000, 'Academy Converter', 0);
GO

-- 3. Multi-Academy Trust
INSERT INTO MultiAcademyTrusts (MultiAcademyTrustID, Name)
VALUES
    (9001, 'Camberwick Academy Trust');
GO

-- 4. Link the academy to the MAT (school is LA-only, so no entry for 9002)
INSERT INTO MultiAcademyTrustEstablishments (MultiAcademyTrustID, EstablishmentID)
VALUES
    (9001, 9003);
GO
