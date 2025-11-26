// Ignore Spelling: Fsm

using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using ApplicationEvidence = CheckYourEligibility.API.Domain.ApplicationEvidence;
using ApplicationStatus = CheckYourEligibility.API.Domain.Enums.ApplicationStatus;
using Establishment = CheckYourEligibility.API.Domain.Establishment;

namespace CheckYourEligibility.API.Gateways;

public class ApplicationGateway : IApplication
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
                    .First(x => x.EstablishmentID == data.Establishment);
                item.LocalAuthorityID = establishment.LocalAuthorityID;
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
            .Where(x => x.ApplicationID == guid)
            .Include(x => x.Statuses)
            .Include(x => x.Establishment)
            .ThenInclude(x => x.LocalAuthority)
            .Include(x => x.User)
            .Include(x => x.EligibilityCheckHash)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync();
        if (result != null)
        {
            var item = _mapper.Map<ApplicationResponse>(result);
            item.CheckOutcome = new ApplicationResponse.ApplicationHash
            { Outcome = result.EligibilityCheckHash?.Outcome.ToString() };
            return item;
        }

        return null;
    }

    public async Task<ApplicationSearchResponse> GetApplications(ApplicationSearchRequest model)
    {
        IQueryable<Application> query;

        if (model.Data.Statuses != null && model.Data.Statuses.Any())
        {
            query = _db.Applications.Where(a => model.Data.Statuses.Contains(a.Status.Value));
        }
        else 
        { 
            query = _db.Applications; 
        }
            
        // Apply other filters
        query = ApplyAdditionalFilters(query, model);

        var totalRecords = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalRecords / model.PageSize);

        // Pagination
        int pageNumber = model.PageNumber <= 0 ? 1 : model.PageNumber;
        if (model.Meta?.PageNumber > pageNumber) pageNumber = model.Meta.PageNumber;
        int pageSize = model.PageSize;
        if (model.Meta?.PageSize > pageSize) pageSize = model.Meta.PageSize;
        var pagedResults = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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
            TotalPages = totalPages,
            Meta = new ApplicationSearchResponseMeta()
            {
                TotalRecords = totalRecords,
                TotalPages = totalPages,
            }
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
    //TODO: This method should live in its own MAT gateway
    public async Task<int> GetLocalAuthorityIdForEstablishment(int establishmentId)
    {
        try
        {
            var localAuthorityId = await _db.Establishments
                .Where(x => x.EstablishmentID == establishmentId)
                .Select(x => x.LocalAuthorityID)
                .FirstAsync();

            return localAuthorityId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to find school:- {establishmentId}");
            throw new NotFoundException($"Unable to find school:- {establishmentId}, {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the multi academy trust ID for an establishment
    /// </summary>
    /// <param name="establishmentId">The establishment ID</param>
    /// <returns>The multi academy trust ID</returns>
    //TODO: This method should live in its own MAT gateway
    public async Task<int> GetMultiAcademyTrustIdForEstablishment(int establishmentId)
    {
        var multiAcademyTrustId = await _db.MultiAcademyTrustEstablishments
            .Where(x => x.EstablishmentID == establishmentId)
            .Select(x => x.MultiAcademyTrustID)
            .FirstOrDefaultAsync();
        return multiAcademyTrustId;
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
                .Select(x => x.LocalAuthorityID)
                .FirstAsync();

            return localAuthorityId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to find application:- {applicationId?.Replace(Environment.NewLine, "")}");
            throw new NotFoundException($"Unable to find application:- {applicationId}, {ex.Message}");
        }
    }

    /// <summary>
    /// Gets establishment information by URN
    /// </summary>
    /// <param name="urn">The establishment URN</param>
    /// <returns>Tuple containing existence flag, establishment ID, and local authority ID</returns>
    //TODO: This method should live in its own Establishment gateway

    public async Task<(bool exists, int establishmentId, int localAuthorityId)> GetEstablishmentByUrn(string urn)
    {
        try
        {
            if (!int.TryParse(urn, out var establishmentId))
            {
                return (false, 0, 0);
            }

            var establishment = await _db.Establishments
                .Where(x => x.EstablishmentID == establishmentId)
                .Select(x => new { x.EstablishmentID, x.LocalAuthorityID })
                .FirstOrDefaultAsync();

            if (establishment == null)
            {
                return (false, 0, 0);
            }

            return (true, establishment.EstablishmentID, establishment.LocalAuthorityID);
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
    //TODO: This method should live in its own Establishment gateway

    public async Task<Establishment?> GetEstablishmentEntityByUrn(string urn)
    {
        if (string.IsNullOrWhiteSpace(urn) || !int.TryParse(urn, out var establishmentId))
        {
            return null;
        }

        return await _db.Establishments
            .FirstOrDefaultAsync(e => e.EstablishmentID == establishmentId);
    }

    /// <summary>
    /// Gets multiple establishment entities by their URNs in bulk
    /// </summary>
    /// <param name="urns">Collection of School URNs as strings</param>
    /// <returns>Dictionary mapping URN to establishment entity</returns>
    //TODO: This method should live in its own Establishment gateway
    public async Task<Dictionary<string, Establishment>> GetEstablishmentEntitiesByUrns(
        IEnumerable<string> urns)
    {
        if (urns == null || !urns.Any())
        {
            return new Dictionary<string, Establishment>();
        }

        // Filter out invalid URNs and convert to integers
        var validUrns = urns
            .Where(urn => !string.IsNullOrWhiteSpace(urn) && int.TryParse(urn, out _))
            .Select(urn => new { OriginalUrn = urn, EstablishmentId = int.Parse(urn) })
            .ToList();

        if (!validUrns.Any())
        {
            return new Dictionary<string, Establishment>();
        }

        // Get all establishments in a single query
        var establishmentIds = validUrns.Select(v => v.EstablishmentId).ToList();
        var establishments = await _db.Establishments
            .Where(e => establishmentIds.Contains(e.EstablishmentID))
            .ToListAsync();

        // Create dictionary mapping original URN string to establishment
        var result = new Dictionary<string, Establishment>();

        foreach (var validUrn in validUrns)
        {
            var establishment = establishments.FirstOrDefault(e => e.EstablishmentID == validUrn.EstablishmentId);
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

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting application {guid?.Replace(Environment.NewLine, "")}");
            throw;
        }
    }


    /// <summary>
    /// Restores an archived application to its most recent non-archived status.
    /// </summary>
    /// <param name="guid">The application GUID to restore.</param>
    /// <returns>An ApplicationStatusRestoreResponse containing the restored status and updated timestamp.</returns>
    public async Task<ApplicationStatusRestoreResponse> RestoreArchivedApplicationStatus(string guid)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(x => x.ApplicationID == guid);

        if (application == null)
            throw new NotFoundException();

        if (application.Status != ApplicationStatus.Archived)
            throw new BadRequest("Only archived applications can be restored");

        var lastStatus = await _db.ApplicationStatuses
            .Where(x => x.ApplicationID == guid && x.Type != ApplicationStatus.Archived)
            .OrderByDescending(x => x.TimeStamp)
            .FirstOrDefaultAsync();


        application.Status = lastStatus.Type;
        application.Updated = DateTime.UtcNow;

        await AddStatusHistory(application, application.Status.Value);
        await _db.SaveChangesAsync();

        return new ApplicationStatusRestoreResponse
        {
            Data = new ApplicationStatusRestoreResponseData
            {
                Status = application.Status.Value.ToString(),
                Updated = application.Updated
            }
        };
    }


    #region Private

    private IQueryable<Application> ApplyAdditionalFilters(IQueryable<Application> query,
        ApplicationSearchRequest model)
    {
        query = query.Where(x => x.Type == model.Data.Type);

        // Clause for specific establishment if provided, or for set of establishments if only MAT provided
        if (model.Data?.Establishment != null)
        {
            query = query.Where(x => x.EstablishmentId == model.Data.Establishment);
        }
        else if (model.Data?.MultiAcademyTrust != null)
        {
            List<int> establishmentIds = GetMatSchoolIds(model.Data.MultiAcademyTrust.Value);
            query = query.Where(x => establishmentIds.Contains(x.EstablishmentId));
        }
        if (model.Data?.LocalAuthority != null)
            query = query.Where(x => x.LocalAuthorityID == model.Data.LocalAuthority);

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
    }

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

    //TODO: Doesn't this exist as a static method elsewhere?
    private EligibilityCheckHash? GetHash(CheckEligibilityType type, Application data)
    {
        var age = DateTime.UtcNow.AddDays(-_hashCheckDays);
        var hash = CheckEligibilityGateway.GetHash(new CheckProcessData
        {
            DateOfBirth = data.ParentDateOfBirth.ToString("yyyy-MM-dd"),
            NationalInsuranceNumber = data.ParentNationalInsuranceNumber,
            NationalAsylumSeekerServiceNumber = data.ParentNationalAsylumSeekerServiceNumber,
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

    //TODO: This method should live in its own MAT gateway
    private List<int> GetMatSchoolIds(int matId)
    {
        return _db.MultiAcademyTrustEstablishments.Where(x => x.MultiAcademyTrustID == matId).Select(x => x.EstablishmentID).ToList();
    }



    #endregion
}