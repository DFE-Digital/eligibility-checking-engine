using System.Globalization;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
/// Interface for bulk deleting applications from CSV or JSON files
/// </summary>
public interface IBulkDeleteApplicationsUseCase
{
    /// <summary>
    /// Executes the bulk deletion of applications from a CSV or JSON file
    /// </summary>
    /// <param name="request">The bulk delete request containing the CSV or JSON file</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing deletion results and any errors</returns>
    Task<ApplicationBulkDeleteResponse> Execute(ApplicationBulkDeleteRequest request, List<int> allowedLocalAuthorityIds);

    /// <summary>
    /// Executes the bulk deletion of applications from JSON body data
    /// </summary>
    /// <param name="request">The bulk delete request containing application GUIDs in JSON format</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing deletion results and any errors</returns>
    Task<ApplicationBulkDeleteResponse> ExecuteFromJson(ApplicationBulkDeleteJsonRequest request, List<int> allowedLocalAuthorityIds);
}

/// <summary>
/// Use case for bulk deleting applications from CSV or JSON files
/// </summary>
public class BulkDeleteApplicationsUseCase : IBulkDeleteApplicationsUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IApplication _applicationGateway;
    private readonly ILogger<BulkDeleteApplicationsUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the BulkDeleteApplicationsUseCase class
    /// </summary>
    /// <param name="applicationGateway">Application gateway for data operations</param>
    /// <param name="auditGateway">Audit gateway for logging</param>
    /// <param name="logger">Logger instance</param>
    public BulkDeleteApplicationsUseCase(
        IApplication applicationGateway,
        IAudit auditGateway,
        ILogger<BulkDeleteApplicationsUseCase> logger)
    {
        _applicationGateway = applicationGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    /// <summary>
    /// Executes the bulk deletion of applications from a CSV or JSON file
    /// </summary>
    /// <param name="request">The bulk delete request containing the CSV or JSON file</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing deletion results and any errors</returns>
    public async Task<ApplicationBulkDeleteResponse> Execute(ApplicationBulkDeleteRequest request, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkDeleteResponse();

        if (request.File == null)
        {
            response.Message = "Delete failed - file is required.";
            response.Errors.Add("File required.");
            return response;
        }

        var contentType = request.File.ContentType?.ToLower();
        var isCSV = contentType == "text/csv" || contentType == "application/csv";
        var isJSON = contentType == "application/json" || contentType == "text/json";

        if (!isCSV && !isJSON)
        {
            response.Message = "Delete failed - CSV or JSON file is required.";
            response.Errors.Add("CSV or JSON file required.");
            return response;
        }

        List<string> applicationGuids;
        try
        {
            if (isCSV)
            {
                applicationGuids = await ParseCSVFile(request.File);
            }
            else // isJSON
            {
                applicationGuids = await ParseJSONFile(request.File);
            }
            
            if (applicationGuids == null || applicationGuids.Count == 0)
            {
                response.Message = "Delete failed - no valid GUIDs found in the file.";
                response.Errors.Add("Invalid file content - no GUIDs found.");
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

        return await ProcessBulkDelete(applicationGuids, allowedLocalAuthorityIds);
    }

    /// <summary>
    /// Executes the bulk deletion of applications from JSON body data
    /// </summary>
    /// <param name="request">The bulk delete request containing application GUIDs in JSON format</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing deletion results and any errors</returns>
    public async Task<ApplicationBulkDeleteResponse> ExecuteFromJson(ApplicationBulkDeleteJsonRequest request, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkDeleteResponse();

        if (request.ApplicationGuids == null || request.ApplicationGuids.Count == 0)
        {
            response.Message = "Delete failed - no application GUIDs provided.";
            response.Errors.Add("Application GUIDs required.");
            return response;
        }

        return await ProcessBulkDelete(request.ApplicationGuids, allowedLocalAuthorityIds);
    }

    /// <summary>
    /// Processes bulk deletion of applications
    /// </summary>
    /// <param name="applicationGuids">List of application GUIDs to delete</param>
    /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs from user claims</param>
    /// <returns>A response containing deletion results and any errors</returns>
    private async Task<ApplicationBulkDeleteResponse> ProcessBulkDelete(List<string> applicationGuids, List<int> allowedLocalAuthorityIds)
    {
        var response = new ApplicationBulkDeleteResponse();

        if (applicationGuids == null || applicationGuids.Count == 0)
        {
            response.Message = "Delete failed - no application GUIDs provided.";
            response.Errors.Add("Application GUIDs required.");
            return response;
        }

        // Remove duplicates and validate GUIDs
        var validGuids = new List<string>();
        var invalidGuids = new List<string>();

        for (int i = 0; i < applicationGuids.Count; i++)
        {
            var guid = applicationGuids[i]?.Trim();
            var rowNumber = i + 1;

            if (string.IsNullOrWhiteSpace(guid))
            {
                response.FailedDeletions++;
                response.Errors.Add($"Row {rowNumber}: Empty or invalid GUID");
                invalidGuids.Add(guid ?? "");
                continue;
            }

            if (!Guid.TryParse(guid, out _))
            {
                response.FailedDeletions++;
                response.Errors.Add($"Row {rowNumber}: Invalid GUID format: {guid}");
                invalidGuids.Add(guid);
                continue;
            }

            if (!validGuids.Contains(guid))
            {
                validGuids.Add(guid);
            }
            else
            {
                response.FailedDeletions++;
                response.Errors.Add($"Row {rowNumber}: Duplicate GUID: {guid}");
            }
        }

        if (!validGuids.Any())
        {
            response.TotalRecords = applicationGuids.Count;
            response.Message = "Delete failed - no valid GUIDs found.";
            return response;
        }

        try
        {
            // Check permissions for all applications in bulk
            var localAuthorityMap = await _applicationGateway.GetLocalAuthorityIdsForApplications(validGuids);
            
            var authorizedGuids = new List<string>();
            var unauthorizedGuids = new List<string>();

            foreach (var guid in validGuids)
            {
                if (!localAuthorityMap.TryGetValue(guid, out var localAuthorityId))
                {
                    // Application not found - will be handled during deletion
                    authorizedGuids.Add(guid);
                    continue;
                }

                // Check local authority permissions
                if (!allowedLocalAuthorityIds.Contains(0) && !allowedLocalAuthorityIds.Contains(localAuthorityId))
                {
                    unauthorizedGuids.Add(guid);
                    response.FailedDeletions++;
                    response.Errors.Add($"GUID {guid}: You do not have permission to delete applications for this establishment's local authority");
                }
                else
                {
                    authorizedGuids.Add(guid);
                }
            }

            if (authorizedGuids.Any())
            {
                // Perform bulk deletion
                var deletionResults = await _applicationGateway.BulkDeleteApplications(authorizedGuids);

                foreach (var result in deletionResults)
                {
                    if (result.Value)
                    {
                        response.SuccessfulDeletions++;
                    }
                    else
                    {
                        response.FailedDeletions++;
                        response.Errors.Add($"GUID {result.Key}: Application not found or could not be deleted");
                    }
                }

                _logger.LogInformation($"Successfully deleted {response.SuccessfulDeletions} applications out of {authorizedGuids.Count} authorized");
            }

            response.TotalRecords = applicationGuids.Count;

            // Set appropriate message based on results
            if (response.SuccessfulDeletions == 0 && response.FailedDeletions > 0)
            {
                response.Message = "Delete failed - all records failed to delete. Please check the errors above.";
            }
            else if (response.SuccessfulDeletions > 0 && response.FailedDeletions == 0)
            {
                response.Message = $"Delete completed successfully - all {response.SuccessfulDeletions} records deleted.";
            }
            else if (response.SuccessfulDeletions > 0 && response.FailedDeletions > 0)
            {
                response.Message = $"Delete partially completed - {response.SuccessfulDeletions} records deleted, {response.FailedDeletions} failed. Please check the errors above.";
            }
            else
            {
                response.Message = "Delete completed - no records to process.";
            }

            await _auditGateway.CreateAuditEntry(AuditType.Administration, string.Empty);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk delete");
            response.Message = "Delete failed - error during bulk database operation.";
            response.Errors.Add($"Error during bulk delete: {ex.Message}");
            
            // Reset counters since the delete failed
            response.FailedDeletions = applicationGuids.Count;
            response.SuccessfulDeletions = 0;
            response.TotalRecords = applicationGuids.Count;
            return response;
        }
    }

    /// <summary>
    /// Parses a CSV file and returns a list of application GUIDs
    /// </summary>
    /// <param name="file">The CSV file to parse</param>
    /// <returns>List of application GUIDs</returns>
    private async Task<List<string>> ParseCSVFile(IFormFile file)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null!,
            MissingFieldFound = null!
        };

        using var fileStream = file.OpenReadStream();
        using var csv = new CsvReader(new StreamReader(fileStream), config);

        var guids = new List<string>();
        
        await foreach (var record in csv.GetRecordsAsync<ApplicationGuidRecord>())
        {
            if (!string.IsNullOrWhiteSpace(record.ApplicationGuid))
            {
                guids.Add(record.ApplicationGuid.Trim());
            }
        }

        return guids;
    }

    /// <summary>
    /// Parses a JSON file and returns a list of application GUIDs
    /// </summary>
    /// <param name="file">The JSON file to parse</param>
    /// <returns>List of application GUIDs</returns>
    private async Task<List<string>> ParseJSONFile(IFormFile file)
    {
        using var fileStream = file.OpenReadStream();
        using var reader = new StreamReader(fileStream);
        var jsonContent = await reader.ReadToEndAsync();

        // Try to parse as array first, then as single object
        try
        {
            var guidsArray = JsonConvert.DeserializeObject<List<string>>(jsonContent);
            if (guidsArray != null)
            {
                return guidsArray.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
            }
        }
        catch
        {
            // If array parsing fails, try parsing as object with ApplicationGuids property
            try
            {
                var guidObject = JsonConvert.DeserializeObject<ApplicationBulkDeleteJsonRequest>(jsonContent);
                if (guidObject?.ApplicationGuids != null)
                {
                    return guidObject.ApplicationGuids.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
                }
            }
            catch
            {
                // If that fails too, try parsing as single string
                var singleGuid = JsonConvert.DeserializeObject<string>(jsonContent);
                if (!string.IsNullOrWhiteSpace(singleGuid))
                {
                    return new List<string> { singleGuid };
                }
            }
        }

        throw new InvalidOperationException("Unable to parse JSON content as list of application GUIDs");
    }

    /// <summary>
    /// CSV record class for application GUID
    /// </summary>
    private class ApplicationGuidRecord
    {
        public string ApplicationGuid { get; set; } = string.Empty;
    }
}
