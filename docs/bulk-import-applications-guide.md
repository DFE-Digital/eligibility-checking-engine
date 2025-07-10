# Bulk Import Documentation

The bulk import functionality now supports both CSV and JSON file formats for importing applications.

## Supported File Formats

### CSV Format
The CSV file should have the following headers in the first row:
- Parent First Name
- Parent Surname  
- Parent DOB
- Parent Nino
- Parent Email Address
- Child First Name
- Child Surname
- Child Date of Birth
- Child School URN
- Eligibility End Date

**Example CSV:**
```csv
Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date
John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31
Sarah,Johnson,1990-07-22,CD789012D,sarah.johnson@example.com,Oliver,Johnson,2016-09-08,150716,2025-08-15
```

### JSON Format
The JSON file can contain either:
1. An array of application objects
2. A single application object

**Example JSON (Array):**
```json
[
  {
    "ParentFirstName": "John",
    "ParentSurname": "Smith",
    "ParentDateOfBirth": "1985-03-15",
    "ParentNino": "AB123456C",
    "ParentEmail": "john.smith@example.com",
    "ChildFirstName": "Emma",
    "ChildSurname": "Smith",
    "ChildDateOfBirth": "2015-04-12",
    "ChildSchoolUrn": "123456",
    "EligibilityEndDate": "2025-07-31"
  },
  {
    "ParentFirstName": "Sarah",
    "ParentSurname": "Johnson",
    "ParentDateOfBirth": "1990-07-22",
    "ParentNino": "CD789012D",
    "ParentEmail": "sarah.johnson@example.com",
    "ChildFirstName": "Oliver",
    "ChildSurname": "Johnson",
    "ChildDateOfBirth": "2016-09-08",
    "ChildSchoolUrn": "150716",
    "EligibilityEndDate": "2025-08-15"
  }
]
```

**Example JSON (Single Object):**
```json
{
  "ParentFirstName": "John",
  "ParentSurname": "Smith",
  "ParentDateOfBirth": "1985-03-15",
  "ParentNino": "AB123456C",
  "ParentEmail": "john.smith@example.com",
  "ChildFirstName": "Emma",
  "ChildSurname": "Smith",
  "ChildDateOfBirth": "2015-04-12",
  "ChildSchoolUrn": "123456",
  "EligibilityEndDate": "2025-07-31"
}
```

## Data Validation

All data is validated regardless of format:

### Required Fields
- Parent First Name
- Parent Surname
- Parent NINO
- Parent Email Address
- Child First Name
- Child Surname
- Child Date of Birth
- Child School URN (must be a positive integer)

### Date Format
All dates must be in the format `yyyy-MM-dd`:
- Parent Date of Birth
- Child Date of Birth
- Eligibility End Date

### Content Types
The API accepts the following content types:
- **CSV:** `text/csv`, `application/csv`
- **JSON:** `application/json`, `text/json`

## API Endpoints

### File Upload Endpoint
**POST** `/application/bulk-import`

**Request:**
- Content-Type: `multipart/form-data`
- Body: Form data with file upload (CSV or JSON file)

### JSON Body Endpoint
**POST** `/application/bulk-import-json`

**Request:**
- Content-Type: `application/json`
- Body: JSON with application data array

**Example Request Body:**
```json
{
  "Applications": [
    {
      "ParentFirstName": "John",
      "ParentSurname": "Smith",
      "ParentDateOfBirth": "1985-03-15",
      "ParentNino": "AB123456C",
      "ParentEmail": "john.smith@example.com",
      "ChildFirstName": "Emma",
      "ChildSurname": "Smith",
      "ChildDateOfBirth": "2015-04-12",
      "ChildSchoolUrn": "123456",
      "EligibilityEndDate": "2025-07-31"
    }
  ]
}
```

**Response (for both endpoints):**
```json
{
  "TotalRecords": 5,
  "SuccessfulImports": 4,
  "FailedImports": 1,
  "Message": "Import partially completed - 4 records imported, 1 failed. Please check the errors above.",
  "Errors": [
    "Row 3: School URN 999999 not found"
  ]
}
```

## Error Messages

The system provides detailed error messages for:
- File format validation
- Missing required fields
- Invalid date formats
- School URN not found
- General parsing errors

## Optional SQL Verification

After performing a bulk import, you can optionally run the SQL verification script to validate that the data was imported correctly:

- `docs/testfiles/verify-bulk-import-applications.sql` - SQL script to verify import results

This script provides:
- Count of total applications
- Recent import verification (last 10 minutes)
- Validation of specific test data
- Establishment mapping verification
- Optional cleanup queries to remove test data

**Note:** The verification script is designed to work with both CSV and JSON imports and includes safety features for test data cleanup.

## Sample Files

Sample files are available in the `docs/testfiles/` directory:
- `sample-bulk-import-applications.csv` - CSV format example
- `sample-bulk-import-applications.json` - JSON format example
- `verify-bulk-import-applications.sql` - Optional verification script
