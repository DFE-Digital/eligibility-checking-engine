# Bulk Delete Applications Guide

This guide explains how to use the bulk delete applications feature to delete multiple applications at once using CSV or JSON files.

## Overview

The bulk delete feature allows you to delete multiple applications by providing their GUIDs (application IDs) in either CSV or JSON format. This is useful when you need to remove multiple applications efficiently.

## Important Notes

- **Admin permissions required**: Only users with admin scope can perform bulk delete operations
- **Local authority restrictions**: You can only delete applications from establishments within your permitted local authorities
- **Irreversible action**: Deleted applications cannot be recovered
- **Validation**: Invalid GUIDs will be reported in the response but won't stop the deletion of valid applications

## Endpoints

### 1. Bulk Delete from File Upload
- **URL**: `POST /application/bulk-delete`
- **Content-Type**: `multipart/form-data`
- **Authorization**: Requires Application, LocalAuthority, and Admin scopes

### 2. Bulk Delete from JSON Body
- **URL**: `POST /application/bulk-delete-json`
- **Content-Type**: `application/json`
- **Authorization**: Requires Application, LocalAuthority, and Admin scopes

## File Formats

### CSV Format

The CSV file should have a header row with a column named `ApplicationGuid`. Each subsequent row should contain one application GUID.

**Example CSV:**
```csv
ApplicationGuid
12345678-1234-1234-1234-123456789abc
87654321-4321-4321-4321-cba987654321
abcdef01-2345-6789-abcd-ef0123456789
```

### JSON Format (File Upload)

For file upload, you can use either:

**1. Simple array of GUIDs:**
```json
[
  "12345678-1234-1234-1234-123456789abc",
  "87654321-4321-4321-4321-cba987654321",
  "abcdef01-2345-6789-abcd-ef0123456789"
]
```

**2. Object with ApplicationGuids property:**
```json
{
  "ApplicationGuids": [
    "12345678-1234-1234-1234-123456789abc",
    "87654321-4321-4321-4321-cba987654321",
    "abcdef01-2345-6789-abcd-ef0123456789"
  ]
}
```

### JSON Format (Request Body)

For the JSON endpoint, use this format:
```json
{
  "ApplicationGuids": [
    "12345678-1234-1234-1234-123456789abc",
    "87654321-4321-4321-4321-cba987654321",
    "abcdef01-2345-6789-abcd-ef0123456789"
  ]
}
```

## Response Format

Both endpoints return the same response format:

```json
{
  "Message": "Delete completed successfully - all 3 records deleted.",
  "TotalRecords": 3,
  "SuccessfulDeletions": 3,
  "FailedDeletions": 0,
  "Errors": []
}
```

### Response Fields

- **Message**: Summary message describing the operation result
- **TotalRecords**: Total number of records processed
- **SuccessfulDeletions**: Number of successfully deleted applications
- **FailedDeletions**: Number of applications that could not be deleted
- **Errors**: Array of error messages for failed deletions

### Example Error Response

```json
{
  "Message": "Delete partially completed - 2 records deleted, 1 failed. Please check the errors above.",
  "TotalRecords": 3,
  "SuccessfulDeletions": 2,
  "FailedDeletions": 1,
  "Errors": [
    "Row 2: Invalid GUID format: invalid-guid",
    "GUID 87654321-4321-4321-4321-cba987654321: Application not found or could not be deleted"
  ]
}
```

## Usage Examples

### Using PowerShell with CSV File

```powershell
$headers = @{
    'Authorization' = 'Bearer YOUR_JWT_TOKEN'
}

$form = @{
    'File' = Get-Item 'applications-to-delete.csv'
}

$response = Invoke-RestMethod -Uri 'https://your-api-url/application/bulk-delete' `
    -Method POST -Headers $headers -Form $form

Write-Output $response
```

### Using PowerShell with JSON

```powershell
$headers = @{
    'Authorization' = 'Bearer YOUR_JWT_TOKEN'
    'Content-Type' = 'application/json'
}

$body = @{
    ApplicationGuids = @(
        "12345678-1234-1234-1234-123456789abc",
        "87654321-4321-4321-4321-cba987654321"
    )
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri 'https://your-api-url/application/bulk-delete-json' `
    -Method POST -Headers $headers -Body $body

Write-Output $response
```

### Using curl with CSV File

```bash
curl -X POST "https://your-api-url/application/bulk-delete" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "File=@applications-to-delete.csv"
```

### Using curl with JSON

```bash
curl -X POST "https://your-api-url/application/bulk-delete-json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "ApplicationGuids": [
      "12345678-1234-1234-1234-123456789abc",
      "87654321-4321-4321-4321-cba987654321"
    ]
  }'
```

## Error Handling

The system validates each GUID and checks permissions before deletion. Common errors include:

- **Invalid GUID format**: The provided string is not a valid GUID
- **Application not found**: No application exists with the given GUID
- **Permission denied**: You don't have permission to delete applications for the establishment's local authority
- **Duplicate GUIDs**: The same GUID appears multiple times in the input
- **Empty GUIDs**: Empty or whitespace-only entries

## Best Practices

1. **Validate GUIDs**: Ensure all GUIDs are properly formatted before bulk deletion
2. **Check permissions**: Verify you have access to all applications before submission
3. **Monitor response**: Always check the response for any failed deletions
4. **Backup considerations**: Consider backing up data before bulk deletion operations
5. **Batch size**: For very large deletions, consider breaking them into smaller batches

## Limitations

- Maximum recommended batch size: 1000 applications per request
- All applications must belong to local authorities you have access to
- Deleted applications cannot be recovered
- Status history and evidence associated with applications will also be deleted

## Troubleshooting

### Common Issues

1. **403 Forbidden**: Check that you have admin scope in your JWT token
2. **400 Bad Request with "No local authority scope found"**: Ensure your JWT token includes local authority scope
3. **Some deletions failed**: Check the Errors array in the response for specific failure reasons
4. **File parsing errors**: Verify your CSV has the correct header and JSON is properly formatted

### Getting Application GUIDs

To get application GUIDs for deletion, you can use the application search endpoint:

```bash
curl -X POST "https://your-api-url/application/search" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "Data": {
      "Type": "FreeSchoolMeals",
      "Statuses": ["SentForReview"]
    },
    "PageNumber": 1,
    "PageSize": 100
  }'
```

The response will include application details with GUIDs that can be used for bulk deletion.
