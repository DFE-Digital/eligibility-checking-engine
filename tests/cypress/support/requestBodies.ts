// TODO: have only client details in the request body

export const validLoginRequestBody = "client_id=".concat(
    Cypress.env('JWT_USERNAME'),
    "&client_secret=",
    encodeURIComponent(Cypress.env('JWT_PASSWORD')),
    "&scope=local_authority check application admin bulk_check establishment user engine"
);

export const validLoginRequestBodyWithClientDetails = "client_id=".concat(
    Cypress.env('JWT_USERNAME'),
    "&client_secret=",
    encodeURIComponent(Cypress.env('JWT_PASSWORD'))
);

export function validHMRCRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'NN123456C',
            lastName: Cypress.env('lastName'),
            dateOfBirth: '2001-01-01',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}

export function invalidHMRCRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'PPG123456C',
            lastName: Cypress.env('lastName'),
            dateOfBirth: '2000-01-01',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}

export function validHomeOfficeRequestBody () {
    return {
        data: {
            nationalInsuranceNumber: '',
            lastName: Cypress.env('lastName'),
            dateOfBirth: '1990-01-01',
            nationalAsylumSeekerServiceNumber: '111111111'
        }
    }
};

export function notEligibleHomeOfficeRequestBody () {
    return { 
        data: {
            nationalInsuranceNumber: '',
            lastName: 'Jacob',
            dateOfBirth: '1990-01-01',
            nationalAsylumSeekerServiceNumber: '110211111'
        }
    }
}

export function invalidDOBRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'AB123456C',
            lastName: 'Smith',
            dateOfBirth: '01/01/19',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}
export function invalidLastNameRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'AB123456C',
            lastName: '',
            dateOfBirth: '2000-01-01',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}

export function noNIAndNASSNRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: '',
            lastName: 'Smith',
            dateOfBirth: '1990-01-01',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}

export function validApplicationSupportRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'NN668767B',
            lastName: Cypress.env('lastName'),
            // lastName: "TESTER",
            dateOfBirth: '1967-03-07',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}

export function validUserRequestBody() {
    return {
        data: {
            email: 'mar@ten.com',
            reference: 'lolz'
        }
    }
}

export function validApplicationRequestBody() {
    return {
        Data: {
            type: "FreeSchoolMeals",
            Establishment: 123456,
            ParentFirstName: "Lebb",
            ParentLastName: Cypress.env('lastName'),
            // ParentLastName: "TESTER",
            ParentNationalInsuranceNumber: "NN668767B",
            ParentNationalAsylumSeekerServiceNumber: null,
            ParentDateOfBirth: "1967-03-07",
            ChildFirstName: "Alexa",
            ChildLastName: "Crittenden",
            ChildDateOfBirth: "2007-08-14",
            UserId: "bc2b0328-9bf6-4a2f-901d-ea694c2b0838",
            ParentEmail :"PostmanTest@test.com",
            Evidence: [
                {
                    "fileName": "Proof_of_Income.pdf",
                    "fileType": "application/pdf",
                    "storageAccountReference": "container/user123/proof_of_income_20250414.pdf"
                },
                {
                    "fileName": "Address_Verification.jpg",
                    "fileType": "image/jpeg",
                    "storageAccountReference": "container/user123/address_verification_20250414.jpg"
                }
            ]
        }
    }
}

// Working Families Single check requests
export function validWorkingFamiliesRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "BB123456D",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "90992385678",
             lastName: "Smith"
        }
    }
}
export function validWorkingFamiliesRequestBodyEligible() {
    return {
        data: {
             nationalInsuranceNumber: "AA123456C",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "90012345671",
             lastName: "TestE"
        }
    }
}
export function validWorkingFamiliesNullLastnameRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "BB123456D",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "50012345678"
        }
    }
}
export function invalidEligiblityCodeRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "BB123456D",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "5001234",
             lastName: "Smith"
        }
    }
}
export function invalidNinoWorkingFamiliesRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "PPG123456C",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "50012345678",
             lastName: "Smith"
        }
    }
}
export function invalidDobWorkingFamiliesRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "BB123456D",
             dateOfBirth: "2022/06/07",
             eligibilityCode: "50012345678",
             lastName: "Smith"
        }
    }
}
export function invalidLastNameWorkingFamiliesRequestBody() {
    return {
        data: {
             nationalInsuranceNumber: "BB123456D",
             dateOfBirth: "2022-06-07",
             eligibilityCode: "50012345678",
             lastName: "Smith1"
        }
    }
}
//  bulk check requests
export function validBulkRequestBody() {
    return {
        data: [
            validHMRCRequestBody().data,
            validHomeOfficeRequestBody().data
        ]
    }
}
export function invalidNinoRequestBody() {
    return {
        data: {
            nationalInsuranceNumber: 'QQ123456A',
            lastName: Cypress.env('lastName'),
            dateOfBirth: '2000-01-01',
            nationalAsylumSeekerServiceNumber: ''
        }
    }
}
export function invalidMultiChecksBulkRequestBody() {
    return {
        data: [
            invalidLastNameRequestBody().data,
            invalidNinoRequestBody().data
        ]
    }
}
export function invalidDateOfBirthBulkCheckRequestBody() {
    return {
        data: [
            validHMRCRequestBody().data,
            invalidDOBRequestBody().data
        ]
    }
}
export function invalidLastNameBulkCheckRequestBody() {
    return {
        data: [
            validHMRCRequestBody().data,
            invalidLastNameRequestBody().data
        ]
    }
}   
export function invalidNinoBulkRequestBody() {
    return {
        data: [
            invalidNinoRequestBody().data,
            validHMRCRequestBody().data

        ]
    }
}

//Working Families Bulk requests
export function validWorkingFamiliesBulkRequestBody() {
    return {
        data: [
            {
                nationalInsuranceNumber: "AA123456C",
                lastName: "Tester",
                dateOfBirth: "2022-06-07",
                eligibilityCode: "90912345671",
                clientIdentifier: 1234
            },
            {
                nationalInsuranceNumber: "BB123456C",
                lastName: "Tester",
                dateOfBirth: "2022-06-07",
                eligibilityCode: "90912345672",
                clientIdentifier: 12345
            },
            {
                nationalInsuranceNumber: "CC123456A",
                lastName: "Tester",
                dateOfBirth: "2022-06-07",
                eligibilityCode: "90922345673",
                clientIdentifier: 123456
            },
            {
                nationalInsuranceNumber: "CC123456A",
                lastName: "Tester",
                dateOfBirth: "2022-06-07",
                eligibilityCode: "90922345674",
                clientIdentifier: 1234567
            }
        ]
    }
}
export function invalidDobWorkingFamiliesBulkRequestBody() {
    return {
        data: [
            validWorkingFamiliesRequestBody().data,
            invalidDobWorkingFamiliesRequestBody().data
        ]
    }
}
export function invalidLastNameWorkingFamiliesBulkRequestBody() {
    return {
        data: [
            validWorkingFamiliesRequestBody().data,
            invalidLastNameWorkingFamiliesRequestBody().data
        ]
    }
}
export function invalidMultiChecksWorkingFamiliesBulkRequestBody() {
    return {
        data: [
            invalidDobWorkingFamiliesRequestBody().data,
            invalidEligiblityCodeRequestBody().data
        ]
    }
}
export function invalidEligiblityCodeBulkRequestBody() {
    return {
        data: [
            validWorkingFamiliesRequestBody().data,
            invalidEligiblityCodeRequestBody().data
        ]
    }
}
export function invalidNinoWorkingFamiliesBulkRequestBody() {
    return {
        data: [
            invalidNinoWorkingFamiliesRequestBody().data,
            validWorkingFamiliesRequestBody().data
        ]
    }
}
