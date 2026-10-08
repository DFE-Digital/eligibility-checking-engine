USE EligibilityCheck;
GO

-- 1. Local Authorities
INSERT INTO LocalAuthorities (LocalAuthorityID, LaName, SchoolCanReviewEvidence)
VALUES
    (201, 'Camden',            1),
    (316, 'Birmingham',        1),
    (330, 'Leeds',             0),
    (894, 'Telford and Wrekin',0);
GO

-- 2. Establishments (FK -> LocalAuthorities)
INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
VALUES
    (100001, 'Camden Primary School',     'NW1 1AA', '1 High Street',     '',            'London',     'Greater London', 1, 201, 'Primary',   0),
    (100002, 'Camden Secondary Academy',  'NW1 2BB', '2 Academy Road',    '',            'London',     'Greater London', 1, 201, 'Secondary', 0),
    (100003, 'Birmingham Infant School',  'B1 1AA',  '10 School Lane',    'Edgbaston',   'Birmingham', 'West Midlands',  1, 316, 'Primary',   0),
    (100004, 'Birmingham High School',    'B2 2BB',  '20 College Street', '',            'Birmingham', 'West Midlands',  1, 316, 'Secondary', 0),
    (100005, 'Leeds Junior School',       'LS1 1AA', '5 Park Avenue',     'City Centre', 'Leeds',      'West Yorkshire', 1, 330, 'Primary',   0),
    (100006, 'Leeds Grammar Academy',     'LS2 2BB', '15 Grammar Road',   '',            'Leeds',      'West Yorkshire', 1, 330, 'Secondary', 0),
    (139766, 'The Telford Langley School',         'TF4 3JS', 'Duce Drive',         'Dawley',        'Telford', 'Shropshire', 1, 894, 'Academy sponsor led',              0),
    (146350, 'Aspris Telford School',               'TF8 7DT', 'Dale Road',          'Coalbrookdale', 'Telford', '',           1, 894, 'Other independent special school', 0),
    (150716, 'The Telford Park School',             'TF3 1FA', 'District Centre',    '',              'Telford', 'Shropshire', 1, 894, 'Academy sponsor led',              1),
    (151706, 'Thomas Telford Primary Free School',  'TF2 5AB', 'George Lees Avenue', '',              'Telford', '',           1, 894, 'Free schools',                     0);
GO

-- 3. Additional Local Authorities (for school search tests)
INSERT INTO LocalAuthorities (LocalAuthorityID, LaName, SchoolCanReviewEvidence)
VALUES
    (208, 'Lewisham',      0),
    (919, 'Hertfordshire', 0);
GO

-- 4. Additional Establishments (for Cypress tests)
-- EstablishmentID 143409: needed by SchoolSearch.cy.ts (search for "Roselands Primary School")
-- EstablishmentID 100718: needed by SchoolSearch.cy.ts (inPrivateBeta = true, "Kilmorie Primary School")
-- EstablishmentID 123456: needed by PostApplication / GetApplication / SearchApplication / UpdateApplicationStatus tests
INSERT INTO Establishments (EstablishmentID, EstablishmentName, Postcode, Street, Locality, Town, County, StatusOpen, LocalAuthorityID, Type, InPrivateBeta)
VALUES
    (143409, 'Roselands Primary School', 'EN11 9AR', 'High Wood Road', '', 'Hoddesdon', 'Hertfordshire', 1, 919, 'Primary',          0),
    (100718, 'Kilmorie Primary School',  'SE23 2SP', 'Kilmorie Road',  '', 'London',    '',              1, 208, 'Community school', 1),
    (123456, 'Test School',              'W1A 1AA',  'Test Street',    '', 'London',    'Greater London', 1, 201, 'Primary',         0);
GO

-- 5. Multi-Academy Trusts
INSERT INTO MultiAcademyTrusts (MultiAcademyTrustID, Name)
VALUES
    (2001, 'Northern Academies Trust'),
    (2002, 'City Learning Partnership');
GO

-- 6. MAT <-> Establishment links (auto-increment PK)
INSERT INTO MultiAcademyTrustEstablishments (MultiAcademyTrustID, EstablishmentID)
VALUES
    (2001, 100005),
    (2001, 100006),
    (2002, 100002),
    (2002, 100004);
GO
