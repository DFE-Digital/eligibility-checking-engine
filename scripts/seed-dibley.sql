-- =============================================================================
-- Dibley FSM Expansion Test Organisation
-- =============================================================================
-- This organisation is reserved for FSM expansion testing only.
-- It must not be used by external or production users.
-- Suitable for DEV / TEST / PREPROD environments.
--
-- Summary:
--   LA     (FSM basic version) : Dibley Council            (LocalAuthorityID = 9004)
--   School (FSM basic version) : Dibley Community School   (EstablishmentID = 9005)
--
-- NOTE: reconstructed from data already present in DEV/TEST (there was no committed
-- seed script for Dibley - see scripts/seed-camberwick.sql for the equivalent MAT/
-- academy setup). No MultiAcademyTrust is associated with Dibley.
-- =============================================================================

USE EligibilityCheck;
GO

-- 1. Local Authority
-- Region = 'Wales (pseudo)' matches Dev - marks Dibley as a pseudo-Welsh LA for testing.
INSERT INTO LocalAuthorities (LocalAuthorityID, LaName, SchoolCanReviewEvidence, EarlyYearsPupilPremiumPolicyID, FreeSchoolMealsPolicyID, TwoYearPolicyID, Region)
VALUES
    (9004, 'Dibley Council', 0, 2, 1, 3, 'Wales (pseudo)');
GO

-- 2. Establishment
INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
VALUES
    (9005, 'Dibley Community School', 'DB1 2BB', '3 Parish Lane', '', 'Dibley', '', 1, 9004, 'Community School', 0);
GO
