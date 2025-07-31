// Ignore Spelling: Fsm

using System.Globalization;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using ApplicationEvidence = CheckYourEligibility.API.Domain.ApplicationEvidence;
using ApplicationStatus = CheckYourEligibility.API.Domain.Enums.ApplicationStatus;

namespace CheckYourEligibility.API.Gateways;

public class ApplicationGateway : BaseGateway, IApplication
{
    private const int referenceMaxValue = 99999999;
    private static Random randomNumber;
    private readonly IEligibilityCheckContext _db;
    private readonly int _hashCheckDays;
    private readonly ILogger _logger;
    protected readonly IMapper _mapper;

    public ApplicationGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext, IMapper mapper,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceCheckEligibility");
        _db = dbContext;
        _mapper = mapper;

        randomNumber ??= new Random(referenceMaxValue);
        _hashCheckDays = configuration.GetValue<short>("HashCheckDays");
    }

    public async Task<ApplicationResponse> PostApplication(ApplicationRequestData data)
    {
        try
        {
            var item = _mapper.Map<Application>(data);
            var hashCheck = GetHash(data.Type, item);
            if (hashCheck == null) throw new Exception($"No Check found. Type:- {data.Type}");
            item.ApplicationID = Guid.NewGuid().ToString();
            item.Type = hashCheck.Type;
            item.Reference = GetReference();
            item.EligibilityCheckHashID = hashCheck?.EligibilityCheckHashID;
            item.Created = DateTime.UtcNow;
            item.Updated = DateTime.UtcNow;

            if (hashCheck.Outcome == CheckEligibilityStatus.eligible)
                item.Status = ApplicationStatus.Entitled;
            else
                item.Status = ApplicationStatus.SentForReview;

            try
            {
                var establishment = _db.Establishments
                    .Include(x => x.LocalAuthority)
                    .First(x => x.EstablishmentId == data.Establishment);
                item.LocalAuthorityId = establishment.LocalAuthorityId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to find school:- {data.Establishment}, {ex.Message}");
            }

            if (data.Evidence != null && data.Evidence.Any())
                foreach (var evidenceItem in data.Evidence)
                    item.Evidence.Add(new ApplicationEvidence
                    {
                        FileName = evidenceItem.FileName,
                        FileType = evidenceItem.FileType,
                        StorageAccountReference = evidenceItem.StorageAccountReference
                    });


            await _db.Applications.AddAsync(item);
            await AddStatusHistory(item, ApplicationStatus.Entitled);

            await _db.SaveChangesAsync();

            var saved = _db.Applications
                .First(x => x.ApplicationID == item.ApplicationID);

            TrackMetric($"Application {item.Type}", 1);

            return await GetApplication(saved.ApplicationID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Db post application");
            throw;
        }
    }


    public async Task<ApplicationResponse?> GetApplication(string guid)
    {
        var result = await _db.Applications
            .Include(x => x.Statuses)
            .Include(x => x.Establishment)
            .ThenInclude(x => x.LocalAuthority)
            .Include(x => x.User)
            .Include(x => x.EligibilityCheckHash)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync(x => x.ApplicationID == guid);
        if (result != null)
        {
            var item = _mapper.Map<ApplicationResponse>(result);
            item.CheckOutcome = new ApplicationResponse.ApplicationHash
                { Outcome = result.EligibilityCheckHash?.Outcome.ToString() };
            return item;
        }

        return null;
    }

    public async Task<ApplicationSearchResponse> GetApplications(ApplicationRequestSearch model)
    {
        IQueryable<Application> query;

        if (model.Data.Statuses != null && model.Data.Statuses.Any())
            query = _db.Applications.Where(a => model.Data.Statuses.Contains(a.Status.Value));
        else
            query = _db.Applications;

        // Apply other filters
        query = ApplyAdditionalFilters(query, model);

        var totalRecords = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalRecords / model.PageSize);

        // Pagination
        model.PageNumber = model.PageNumber <= 0 ? 1 : model.PageNumber;
        var pagedResults = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Include(x => x.Statuses)
            .Include(x => x.Establishment)
            .ThenInclude(x => x.LocalAuthority)
            .Include(x => x.User)
            .Include(x => x.Evidence)
            .ToListAsync();


        var mappedResults = _mapper.Map<IEnumerable<ApplicationResponse>>(pagedResults);

        return new ApplicationSearchResponse
        {
            Data = mappedResults,
            TotalRecords = totalRecords,
            TotalPages = totalPages
        };
    }

    public async Task<ApplicationStatusUpdateResponse> UpdateApplicationStatus(string guid, ApplicationStatusData data)
    {
        var result = await _db.Applications.FirstOrDefaultAsync(x => x.ApplicationID == guid);
        if (result != null)
        {
            result.Status = data.Status;
            await AddStatusHistory(result, result.Status.Value);

            result.Updated = DateTime.UtcNow;
            var updates = await _db.SaveChangesAsync();
            TrackMetric($"Application Status Change {result.Status}", 1);
            TrackMetric($"Application Status Change Establishment:-{result.EstablishmentId} {result.Status}", 1);
            TrackMetric($"Application Status Change La:-{result.LocalAuthorityId} {result.Status}", 1);
            return new ApplicationStatusUpdateResponse
                { Data = new ApplicationStatusDataResponse { Status = result.Status.Value.ToString() } };
        }

        return null;
    }

    /// <summary>
    /// Gets the local authority ID for an establishment
    /// </summary>
    /// <param name="establishmentId">The establishment ID</param>
    /// <returns>The local authority ID</returns>
    public async Task<int> GetLocalAuthorityIdForEstablishment(int establishmentId)
    {
        try
        {
            var localAuthorityId = await _db.Establishments
                .Where(x => x.EstablishmentId == establishmentId)
                .Select(x => x.LocalAuthorityId)
                .FirstAsync();

            return localAuthorityId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to find school:- {establishmentId}");
            throw new Exception($"Unable to find school:- {establishmentId}, {ex.Message}");
        }
    }

    /// <summary>
    /// Get the local authority ID based on application ID
    /// </summary>
    /// <param name="applicationId">The application ID</param>
    /// <returns>The local authority ID</returns>
    public async Task<int> GetLocalAuthorityIdForApplication(string applicationId)
    {
        try
        {
            var localAuthorityId = await _db.Applications
                .Where(x => x.ApplicationID == applicationId)
                .Select(x => x.LocalAuthorityId)
                .FirstAsync();

            return localAuthorityId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to find application:- {applicationId?.Replace(Environment.NewLine, "")}");
            throw new Exception($"Unable to find application:- {applicationId}, {ex.Message}");
        }
    }

    /// <summary>
    /// Gets establishment information by URN
    /// </summary>
    /// <param name="urn">The establishment URN</param>
    /// <returns>Tuple containing existence flag, establishment ID, and local authority ID</returns>
    public async Task<(bool exists, int establishmentId, int localAuthorityId)> GetEstablishmentByUrn(string urn)
    {
        try
        {
            if (!int.TryParse(urn, out var establishmentId))
            {
                return (false, 0, 0);
            }

            var establishment = await _db.Establishments
                .Where(x => x.EstablishmentId == establishmentId)
                .Select(x => new { x.EstablishmentId, x.LocalAuthorityId })
                .FirstOrDefaultAsync();

            if (establishment == null)
            {
                return (false, 0, 0);
            }

            return (true, establishment.EstablishmentId, establishment.LocalAuthorityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error finding establishment with URN: {urn}");
            return (false, 0, 0);
        }
    }

    /// <summary>
    /// Bulk imports applications without creating eligibility check hashes
    /// </summary>
    /// <param name="applications">Collection of applications to import</param>
    /// <returns>Task</returns>
    public Task BulkImportApplications(IEnumerable<Application> applications)
    {
        try
        {
            var applicationsList = applications.ToList();

            if (!applicationsList.Any())
            {
                _logger.LogInformation("No applications to import");
                return Task.CompletedTask;
            }

            _logger.LogInformation($"Starting bulk import of {applicationsList.Count} applications");

            // Use the bulk insert method from the context
            _db.BulkInsert_Applications(applicationsList);

            _logger.LogInformation($"Successfully imported {applicationsList.Count} applications");

            // Track metrics
            TrackMetric("Bulk Applications Imported", applicationsList.Count);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk application import");
            throw;
        }
    }

    /// <summary>
    /// Gets establishment entity by URN (Unique Reference Number)
    /// </summary>
    /// <param name="urn">School URN as string</param>
    /// <returns>Establishment entity or null if not found</returns>
    public async Task<CheckYourEligibility.API.Domain.Establishment?> GetEstablishmentEntityByUrn(string urn)
    {
        if (string.IsNullOrWhiteSpace(urn) || !int.TryParse(urn, out var establishmentId))
        {
            return null;
        }

        return await _db.Establishments
            .FirstOrDefaultAsync(e => e.EstablishmentId == establishmentId);
    }

    /// <summary>
    /// Gets multiple establishment entities by their URNs in bulk
    /// </summary>
    /// <param name="urns">Collection of School URNs as strings</param>
    /// <returns>Dictionary mapping URN to establishment entity</returns>
    public async Task<Dictionary<string, CheckYourEligibility.API.Domain.Establishment>> GetEstablishmentEntitiesByUrns(
        IEnumerable<string> urns)
    {
        if (urns == null || !urns.Any())
        {
            return new Dictionary<string, CheckYourEligibility.API.Domain.Establishment>();
        }

        // Filter out invalid URNs and convert to integers
        var validUrns = urns
            .Where(urn => !string.IsNullOrWhiteSpace(urn) && int.TryParse(urn, out _))
            .Select(urn => new { OriginalUrn = urn, EstablishmentId = int.Parse(urn) })
            .ToList();

        if (!validUrns.Any())
        {
            return new Dictionary<string, CheckYourEligibility.API.Domain.Establishment>();
        }

        // Get all establishments in a single query
        var establishmentIds = validUrns.Select(v => v.EstablishmentId).ToList();
        var establishments = await _db.Establishments
            .Where(e => establishmentIds.Contains(e.EstablishmentId))
            .ToListAsync();

        // Create dictionary mapping original URN string to establishment
        var result = new Dictionary<string, CheckYourEligibility.API.Domain.Establishment>();

        foreach (var validUrn in validUrns)
        {
            var establishment = establishments.FirstOrDefault(e => e.EstablishmentId == validUrn.EstablishmentId);
            if (establishment != null)
            {
                result[validUrn.OriginalUrn] = establishment;
            }
        }

        return result;
    }

    /// <summary>
    /// Deletes an application by GUID
    /// </summary>
    /// <param name="guid">Application GUID</param>
    /// <returns>True if deleted successfully, false if not found</returns>
    public async Task<bool> DeleteApplication(string guid)
    {
        try
        {
            var application = await _db.Applications
                .Include(x => x.Statuses)
                .Include(x => x.Evidence)
                .FirstOrDefaultAsync(x => x.ApplicationID == guid);

            if (application == null)
            {
                return false;
            }

            // Remove related status history
            if (application.Statuses.Any())
            {
                _db.ApplicationStatuses.RemoveRange(application.Statuses);
            }

            // Remove the application
            _db.Applications.Remove(application);
            
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Application {guid.Replace(Environment.NewLine, "")} deleted successfully");
            TrackMetric("Application Deleted", 1);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting application {guid?.Replace(Environment.NewLine, "")}");
            throw;
        }
    }

    #region Private

    private IQueryable<Application> ApplyAdditionalFilters(IQueryable<Application> query,
        ApplicationRequestSearch model)
    {
        query = query.Where(x => x.Type == model.Data.Type);

        if (model.Data?.Establishment != null)
            query = query.Where(x => x.EstablishmentId == model.Data.Establishment);
        if (model.Data?.LocalAuthority != null)
            query = query.Where(x => x.LocalAuthorityId == model.Data.LocalAuthority);

        if (!string.IsNullOrEmpty(model.Data?.ParentNationalInsuranceNumber))
            query = query.Where(x => x.ParentNationalInsuranceNumber == model.Data.ParentNationalInsuranceNumber);
        if (!string.IsNullOrEmpty(model.Data?.ParentLastName))
            query = query.Where(x => x.ParentLastName == model.Data.ParentLastName);
        if (!string.IsNullOrEmpty(model.Data?.ParentNationalAsylumSeekerServiceNumber))
            query = query.Where(x =>
                x.ParentNationalAsylumSeekerServiceNumber == model.Data.ParentNationalAsylumSeekerServiceNumber);
        if (!string.IsNullOrEmpty(model.Data?.ParentDateOfBirth))
            query = query.Where(x =>
                x.ParentDateOfBirth == DateTime.ParseExact(model.Data.ParentDateOfBirth, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(model.Data?.ChildLastName))
            query = query.Where(x => x.ChildLastName == model.Data.ChildLastName);
        if (!string.IsNullOrEmpty(model.Data?.ChildDateOfBirth))
            query = query.Where(x =>
                x.ChildDateOfBirth == DateTime.ParseExact(model.Data.ChildDateOfBirth, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(model.Data?.Reference))
            query = query.Where(x => x.Reference == model.Data.Reference);
        if (model.Data?.DateRange != null)
            query = query.Where(x =>
                x.Created > model.Data.DateRange.DateFrom && x.Created < model.Data.DateRange.DateTo);
        if (!string.IsNullOrEmpty(model.Data?.Keyword))
        {
            string[] keywords = model.Data.Keyword.Split(' ');
            foreach (var keyword in keywords)
                query = query.Where(
                    x =>
                        x.Reference.Contains(keyword) ||
                        x.ChildFirstName.Contains(keyword) ||
                        x.ChildLastName.Contains(keyword) ||
                        x.ParentFirstName.Contains(keyword) ||
                        x.ParentLastName.Contains(keyword) ||
                        x.ParentNationalInsuranceNumber.Contains(keyword) ||
                        x.ParentNationalAsylumSeekerServiceNumber.Contains(keyword) ||
                        x.ParentEmail.Contains(keyword) ||
                        x.Establishment.EstablishmentName.Contains(keyword)
                );
        }

        return query.OrderBy(x => x.Created);

        // In case we need to order by status first and then by created date
        /* if (model.Data?.Statuses != null && model.Data.Statuses.Any())
        {
            return query.OrderBy(x => x.Status).ThenBy(x => x.Created);
        }
        else
        {
            return query.OrderBy(x => x.Created);
        } */
    }


    /* private string GetReference()
    {
        var unique = false;
        var nextReference = string.Empty;
        while (!unique)
        {
            nextReference = randomNumber.Next(1, referenceMaxValue).ToString();
            unique = _db.Applications.FirstOrDefault(x => x.Reference == nextReference) == null;
        }

        return nextReference;
    } */

    private string GetReference()
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            // timestamp ticks to genereate a reference
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            var reference = timestamp.Substring(timestamp.Length - 8);

            if (_db.Applications.FirstOrDefault(x => x.Reference == reference) == null) return reference;

            // Reference exists, wait a bit and try again
            Task.Delay(5).Wait();
        }

        // Fallback: add a random suffix to virtually guarantee uniqueness
        var finalTimestamp = DateTime.UtcNow.Ticks.ToString();
        var randomSuffix = randomNumber.Next(10, 100).ToString();
        var fallbackReference = finalTimestamp.Substring(finalTimestamp.Length - 6) + randomSuffix;

        // safe check for uniqueness
        if (_db.Applications.FirstOrDefault(x => x.Reference == fallbackReference) == null) return fallbackReference;

        // Final fallback
        return Guid.NewGuid().ToString("N").Substring(0, 8);
    }


    private EligibilityCheckHash? GetHash(CheckEligibilityType type, Application data)
    {
        var age = DateTime.UtcNow.AddDays(-_hashCheckDays);
        var hash = CheckEligibilityGateway.GetHash(new CheckProcessData
        {
            DateOfBirth = data.ParentDateOfBirth.ToString("yyyy-MM-dd"),
            NationalInsuranceNumber = data.ParentNationalAsylumSeekerServiceNumber,
            NationalAsylumSeekerServiceNumber = data.ParentNationalInsuranceNumber,
            LastName = data.ParentLastName.ToUpper(),
            Type = type
        });
        return _db.EligibilityCheckHashes.FirstOrDefault(x => x.Hash == hash && x.TimeStamp >= age);
    }

    private async Task AddStatusHistory(Application application, ApplicationStatus applicationStatus)
    {
        var status = new Domain.ApplicationStatus
        {
            ApplicationStatusID = Guid.NewGuid().ToString(),
            ApplicationID = application.ApplicationID,
            Type = applicationStatus,
            TimeStamp = DateTime.UtcNow
        };
        await _db.ApplicationStatuses.AddAsync(status);
    }

    #endregion
}