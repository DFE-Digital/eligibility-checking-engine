
-- =============================================================================
-- DSI Organisation table – Camberwick test organisations
-- =============================================================================
-- Run these queries against the DSI database (table: Organisation).
-- Organisations are looked up by the "name" column (lowercase).
--
-- Fields updated:
--   LegalName           – display / legal name
--   UPIN                – unique provider identifier
--   EstablishmentNumber – numeric identifier (matches ECE LocalAuthorityID /
--                         MultiAcademyTrustID / EstablishmentID)
--   localAuthorityCode  – LA code for school/academy rows (from ECE LocalAuthorityID)
--   localAuthorityName  – LA name for school/academy rows
--   localAuthorityId    – DSI internal ID of the Camberwick Council org
--                         (resolved via sub-select – see schools below)
--
-- NOTE: "Type" was omitted because the correct DSI type codes for these
--       organisations are not known. Please confirm with the DSI team and
--       add a SET Type = '<code>' to each UPDATE if needed.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- UPDATE queries
-- -----------------------------------------------------------------------------

-- 1. Camberwick Council (LA)
UPDATE Organisation
SET
    LegalName           = 'CAMBERWICK COUNCIL',
    UPIN                = '999000',
    EstablishmentNumber = '9000'
WHERE name = 'CAMBERWICK COUNCIL';
GO

-- 2. Camberwick Academy Trust (MAT)
UPDATE Organisation
SET
    LegalName           = 'CAMBERWICK ACADEMY TRUST',
    UPIN                = '999001',
    EstablishmentNumber = '9001'
WHERE name = 'CAMBERWICK ACADEMY TRUST';
GO

-- 3. Camberwick Community School
--    localAuthorityId is the DSI internal ID of the Camberwick Council org,
--    resolved by sub-select; replace the sub-select with a literal value if
--    you already know it.
UPDATE Organisation
SET
    LegalName           = 'CAMBERWICK COMMUNITY SCHOOL',
    UPIN                = '999002',
    EstablishmentNumber = '9002',
    localAuthorityId    = (SELECT id FROM Organisation WHERE name = 'CAMBERWICK COUNCIL'),
    localAuthorityCode  = '9000',
    localAuthorityName  = 'Camberwick Council'
WHERE name = 'CAMBERWICK COMMUNITY SCHOOL';
GO

-- 4. Camberwick Academy
UPDATE Organisation
SET
    LegalName           = 'CAMBERWICK ACADEMY',
    UPIN                = '999003',
    EstablishmentNumber = '9003',
    localAuthorityId    = (SELECT id FROM Organisation WHERE name = 'CAMBERWICK COUNCIL'),
    localAuthorityCode  = '9000',
    localAuthorityName  = 'Camberwick Council'
WHERE name = 'CAMBERWICK ACADEMY';
GO

-- -----------------------------------------------------------------------------
-- SELECT queries – run after the updates to verify
-- -----------------------------------------------------------------------------

-- All four organisations at once
SELECT
    name,
    LegalName,
    UPIN,
    EstablishmentNumber,
    localAuthorityId,
    localAuthorityCode,
    localAuthorityName
FROM Organisation
WHERE name IN (
    'CAMBERWICK COUNCIL',
    'CAMBERWICK ACADEMY TRUST',
    'CAMBERWICK COMMUNITY SCHOOL',
    'CAMBERWICK ACADEMY'
);

-- Individual checks (useful for targeted debugging)

SELECT name, LegalName, UPIN, EstablishmentNumber
FROM Organisation WHERE name = 'CAMBERWICK COUNCIL';

SELECT name, LegalName, UPIN, EstablishmentNumber
FROM Organisation WHERE name = 'CAMBERWICK ACADEMY TRUST';

SELECT name, LegalName, UPIN, EstablishmentNumber, localAuthorityId, localAuthorityCode, localAuthorityName
FROM Organisation WHERE name = 'CAMBERWICK COMMUNITY SCHOOL';

SELECT name, LegalName, UPIN, EstablishmentNumber, localAuthorityId, localAuthorityCode, localAuthorityName
FROM Organisation WHERE name = 'CAMBERWICK ACADEMY';
