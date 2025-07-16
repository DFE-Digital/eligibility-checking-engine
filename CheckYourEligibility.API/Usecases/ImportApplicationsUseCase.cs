using System.Globalization;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ApplicationStatus = CheckYourEligibility.API.Domain.Enums.ApplicationStatus;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for importing applications in bulk from CSV or JSON files
/// </summary>
public interface IImportApplicationsUseCase
{
    /// <summary>
    /// Executes the bulk import of applications from a CSV or JSON file
    /// </summary>
    /// <param name="request">The bulk import request containing the CSV or JSON file</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing import results and any errors</returns>
    Task<ApplicationBulkImportResponse> Execute(ApplicationBulkImportRequest request, List<int> allowedLocalAuthorityIds);

    /// <summary>
    /// Executes the bulk import of applications from JSON body data
    /// </summary>
    /// <param name="request">The bulk import request containing application data in JSON format</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing import results and any errors</returns>
    Task<ApplicationBulkImportResponse> ExecuteFromJson(ApplicationBulkImportJsonRequest request, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Use case for importing applications in bulk from CSV or JSON files
/// </summary>
public class ImportApplicationsUseCase : IImportApplicationsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IApplication _applicationGateway;
    private readonly ILogger<ImportApplicationsUseCase> _logger;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the ImportApplicationsUseCase class
    /// </summary>
    /// <param name="applicationGateway">Application gateway for data operations</param>
    /// <param name="auditGateway">Audit gateway for logging</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="mapper">AutoMapper instance</param>
    public ImportApplicationsUseCase(
        IApplication applicationGateway,
        IAudit auditGateway,
        ILogger<ImportApplicationsUseCase> logger,
        IMapper mapper)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
        _logger = logger;
        _mapper = mapper;
    }    /// <summary>
         /// Executes the bulk import of applications from a CSV or JSON file
         /// </summary>
         /// <param name="request">The bulk import request containing the CSV or JSON file</param>
         /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
         /// <returns>A response containing import results and any errors</returns>
    public async Task<ApplicationBulkImportResponse> Execute(ApplicationBulkImportRequest request, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkImportResponse();

        if (request.File == null)
        {
            response.Message = "Import failed - file is required.";
            response.Errors.Add("File required.");
            return response;
        }

        var contentType = request.File.ContentType?.ToLower();
        var isCSV = contentType == "text/csv" || contentType == "application/csv";
        var isJSON = contentType == "application/json" || contentType == "text/json";

        if (!isCSV && !isJSON)
        {
            response.Message = "Import failed - CSV or JSON file is required.";
            response.Errors.Add("CSV or JSON file required.");
            return response;
        }

        List<ApplicationBulkImportRow> importData;
        try
        {
            if (isCSV)
            {
                importData = await ParseCSVFile(request.File);
            }
            else // isJSON
            {
                importData = await ParseJSONFile(request.File);
            }
            if (importData == null || importData.Count == 0)
            {
                response.Message = "Import failed - no valid records found in the file.";
                response.Errors.Add("Invalid file content - no records found.");
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file");
            response.Message = $"Error parsing {(isCSV ? "CSV" : "JSON")} file";
            response.Errors.Add($"Error parsing {(isCSV ? "CSV" : "JSON")} file: {ex.Message}");
            return response;
        }

        // Use shared processing logic
        return await ProcessImportData(importData, allowedLocalAuthorityIds);
    }

    /// <summary>
    /// Executes the bulk import of applications from JSON body data
    /// </summary>
    /// <param name="request">The bulk import request containing application data in JSON format</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing import results and any errors</returns>
    public async Task<ApplicationBulkImportResponse> ExecuteFromJson(ApplicationBulkImportJsonRequest request, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkImportResponse();

        if (request.Applications == null || request.Applications.Count == 0)
        {
            response.Message = "Import failed - no application data provided.";
            response.Errors.Add("Application data required.");
            return response;
        }
        // Convert JSON data to import rows (reusing existing logic)
        var importData = request.Applications.Select(ConvertToImportRow).ToList();

        // Use shared processing logic
        return await ProcessImportData(importData, allowedLocalAuthorityIds);
    }

    /// <summary>
    /// Processes import data and creates applications
    /// </summary>
    /// <param name="importData">List of application import rows</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing import results and any errors</returns>
    private async Task<ApplicationBulkImportResponse> ProcessImportData(List<ApplicationBulkImportRow> importData, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkImportResponse();

        if (importData == null || importData.Count == 0)
        {
            response.Message = "Import failed - no valid records found in the data.";
            response.Errors.Add("Invalid data content - no records found.");
            return response;
        }

        var applications = new List<Application>();

        // First, validate all rows and collect valid URNs
        var validRows = new List<(ApplicationBulkImportRow row, int rowNumber, ValidationResult validation)>();

        // In test cases, rows are counted starting from 1 (for first data row)
        for (int rowIndex = 0; rowIndex < importData.Count; rowIndex++)
        {
            var row = importData[rowIndex];
            var rowDisplayNumber = rowIndex + 1; // For display in error messages

            var validationResult = ValidateRow(row);
            if (validationResult.IsValid)
            {
                validRows.Add((row, rowDisplayNumber, validationResult));
            }
            else
            {
                response.FailedImports++;
                response.Errors.Add($"Row {rowDisplayNumber}: {validationResult.ErrorMessage}");
            }
        }

        // Get all establishments in bulk for better performance
        var uniqueUrns = validRows.Select(vr => vr.row.ChildSchoolUrn).Distinct().ToList();
        var establishmentLookup = await _applicationGateway.GetEstablishmentEntitiesByUrns(uniqueUrns);

        // Process each validated row
        foreach (var (row, rowNum, validationResult) in validRows)
        {
            try
            {
                // Check if establishment exists in our bulk lookup
                if (!establishmentLookup.TryGetValue(row.ChildSchoolUrn, out var establishment))
                {
                    response.FailedImports++;
                    response.Errors.Add($"Row {rowNum}: Establishment with URN {row.ChildSchoolUrn} not found");
                    continue;
                }

                // Check local authority permissions - similar to CreateApplicationUseCase
                if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(establishment.LocalAuthorityId))
                {
                    response.FailedImports++;
                    response.Errors.Add($"Row {rowNum}: You do not have permission to create applications for this establishment's local authority");
                    continue;
                }

                var application = new Application
                {
                    ApplicationID = Guid.NewGuid().ToString(),
                    Type = CheckEligibilityType.FreeSchoolMeals, // Default to FSM for bulk import
                    Reference = GenerateReference(),
                    LocalAuthorityId = establishment.LocalAuthorityId,
                    EstablishmentId = establishment.EstablishmentId,
                    ParentFirstName = row.ParentFirstName,
                    ParentLastName = row.ParentSurname,
                    ParentDateOfBirth = validationResult.ParentDateOfBirth!.Value,
                    ParentNationalInsuranceNumber = row.ParentNino,
                    ParentEmail = row.ParentEmail,
                    ChildFirstName = row.ChildFirstName,
                    ChildLastName = row.ChildSurname,
                    ChildDateOfBirth = validationResult.ChildDateOfBirth!.Value,
                    EligibilityEndDate = validationResult.EligibilityEndDate!.Value,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    // If ApplicationStatus is not provided, set Status to Receiving else to whatever is provided
                    Status = string.IsNullOrWhiteSpace(row.ApplicationStatus)
                        ? ApplicationStatus.Receiving
                        : Enum.Parse<ApplicationStatus>(row.ApplicationStatus),
                    EligibilityCheckHashID = null // No hash for bulk import
                };
                applications.Add(application);
                response.SuccessfulImports++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing row {rowNum}");
                response.FailedImports++;
                response.Errors.Add($"Row {rowNum}: Error processing record - {ex.Message}");
            }
        }

        if (applications.Any())
        {
            try
            {
                await _applicationGateway.BulkImportApplications(applications);
                _logger.LogInformation($"Successfully imported {applications.Count} applications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk import");
                response.Message = "Import failed - error during bulk database operation.";
                response.Errors.Add($"Error during bulk import: {ex.Message}");
                // Reset counters since the import failed
                response.FailedImports = importData.Count;
                response.SuccessfulImports = 0;
                return response;
            }
        }
        response.TotalRecords = importData.Count;

        // Set appropriate message based on results
        if (response.SuccessfulImports == 0 && response.FailedImports > 0)
        {
            response.Message = "Import failed - all records failed to import. Please check the errors above.";
        }
        else if (response.SuccessfulImports > 0 && response.FailedImports == 0)
        {
            response.Message = $"Import completed successfully - all {response.SuccessfulImports} records imported.";
        }
        else if (response.SuccessfulImports > 0 && response.FailedImports > 0)
        {
            response.Message = $"Import partially completed - {response.SuccessfulImports} records imported, {response.FailedImports} failed. Please check the errors above.";
        }
        else
        {
            response.Message = "Import completed - no records to process.";
        }

        await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
        return response;
    }

    /// <summary>
    /// Parses a CSV file and returns a list of ApplicationBulkImportRow objects
    /// </summary>
    /// <param name="file">The CSV file to parse</param>
    /// <returns>List of ApplicationBulkImportRow objects</returns>
    private async Task<List<ApplicationBulkImportRow>> ParseCSVFile(IFormFile file)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null!,
            MissingFieldFound = null!
        };

        using var fileStream = file.OpenReadStream();
        using var csv = new CsvReader(new StreamReader(fileStream), config);

        csv.Context.RegisterClassMap<ApplicationBulkImportRowMap>();
        return await Task.FromResult(csv.GetRecords<ApplicationBulkImportRow>().ToList());
    }

    /// <summary>
    /// Parses a JSON file and returns a list of ApplicationBulkImportRow objects
    /// </summary>
    /// <param name="file">The JSON file to parse</param>
    /// <returns>List of ApplicationBulkImportRow objects</returns>
    private async Task<List<ApplicationBulkImportRow>> ParseJSONFile(IFormFile file)
    {
        using var fileStream = file.OpenReadStream();
        using var reader = new StreamReader(fileStream);
        var jsonContent = await reader.ReadToEndAsync();

        // Try to parse as array first, then as single object
        try
        {
            var dataArray = JsonConvert.DeserializeObject<List<ApplicationBulkImportData>>(jsonContent);
            if (dataArray != null)
            {
                return dataArray.Select(ConvertToImportRow).ToList();
            }
        }
        catch
        {
            // If array parsing fails, try parsing as single object
            var singleData = JsonConvert.DeserializeObject<ApplicationBulkImportData>(jsonContent);
            if (singleData != null)
            {
                return new List<ApplicationBulkImportRow> { ConvertToImportRow(singleData) };
            }
        }

        throw new InvalidOperationException("Unable to parse JSON content as ApplicationBulkImportData");
    }    /// <summary>
    /// Converts ApplicationBulkImportData to ApplicationBulkImportRow
    /// </summary>
    /// <param name="data">The data object to convert</param>
    /// <returns>ApplicationBulkImportRow object</returns>
    private ApplicationBulkImportRow ConvertToImportRow(ApplicationBulkImportData data)
    {
        return new ApplicationBulkImportRow
        {
            ParentFirstName = data.ParentFirstName,
            ParentSurname = data.ParentSurname,
            ParentDOB = data.ParentDateOfBirth,
            ParentNino = data.ParentNino,
            ParentEmail = data.ParentEmail,
            ChildFirstName = data.ChildFirstName,
            ChildSurname = data.ChildSurname,
            ChildDateOfBirth = data.ChildDateOfBirth,
            ChildSchoolUrn = data.ChildSchoolUrn,
            EligibilityEndDate = data.EligibilityEndDate,
            ApplicationStatus = data.ApplicationStatus
        };
    }    private ValidationResult ValidateRow(ApplicationBulkImportRow row)
    {
        var result = new ValidationResult { IsValid = true, ErrorMessages = new List<string>() };

        // Validate required fields
        if (string.IsNullOrWhiteSpace(row.ParentFirstName))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Parent first name is required");
        }

        if (string.IsNullOrWhiteSpace(row.ParentSurname))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Parent surname is required");
        }

        if (string.IsNullOrWhiteSpace(row.ParentNino))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Parent NINO is required");
        }

        if (string.IsNullOrWhiteSpace(row.ParentEmail))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Parent Email Address is required");
        }

        if (string.IsNullOrWhiteSpace(row.ChildFirstName))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Child First Name is required");
        }

        if (string.IsNullOrWhiteSpace(row.ChildSurname))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Child Surname is required");
        }

        if (string.IsNullOrWhiteSpace(row.ChildDateOfBirth))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Child Date of Birth is required");
        }

        if (string.IsNullOrWhiteSpace(row.ChildSchoolUrn) || !int.TryParse(row.ChildSchoolUrn, out var urn) || urn <= 0)
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Child School URN is required and must be greater than 0");
        }

        // Validate dates
        if (!DateTime.TryParseExact(row.ParentDOB, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parentDob))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Parent date of birth must be in format yyyy-MM-dd");
        }
        else
        {
            result.ParentDateOfBirth = parentDob;
        }

        if (!DateTime.TryParseExact(row.ChildDateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var childDob))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Child date of birth must be in format yyyy-MM-dd");
        }
        else
        {
            result.ChildDateOfBirth = childDob;
        }

        if (!DateTime.TryParseExact(row.EligibilityEndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var eligibilityEndDate))
        {
            result.IsValid = false;
            result.ErrorMessages.Add("Eligibility End Date must be in format yyyy-MM-dd");
        }
        else
        {
            result.EligibilityEndDate = eligibilityEndDate;
        }

        // Combine all error messages into a single string for backward compatibility
        if (result.ErrorMessages.Any())
        {
            result.ErrorMessage = string.Join("; ", result.ErrorMessages);
        }

        return result;
    }

    private string GenerateReference()
    {
        // Generate a simple reference based on timestamp
        var timestamp = DateTime.UtcNow.Ticks.ToString();
        return timestamp.Substring(timestamp.Length - 8);
    }    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public DateTime? ParentDateOfBirth { get; set; }
        public DateTime? ChildDateOfBirth { get; set; }
        public DateTime? EligibilityEndDate { get; set; }
    }
}
