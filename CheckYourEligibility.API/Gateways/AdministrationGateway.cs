// Ignore Spelling: Fsm

using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Gateways;

public class AdministrationGateway : IAdministration
{
    private readonly IConfiguration _configuration;
    private readonly IEligibilityCheckContext _db;
    private readonly ILogger _logger;

    public AdministrationGateway(ILoggerFactory logger, IEligibilityCheckContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceAdministration");
        _db = dbContext;
        _configuration = configuration;
    }

    public async Task CleanUpEligibilityChecks()
    {
        var eligibileRetentionDays = _configuration.GetValue<int>($"DataCleanseDaysSoftCheck_Status_{CheckEligibilityStatus.eligible}");
        if (eligibileRetentionDays > 0)
        {
            var checkDate =
                DateTime.UtcNow.AddDays(
                    -eligibileRetentionDays);
            var items = _db.CheckEligibilities.Where(x => x.Created <= checkDate);
            _db.CheckEligibilities.RemoveRange(items);
            await _db.SaveChangesAsync();
        }

        var notFoundRetentionDays = _configuration.GetValue<int>($"DataCleanseDaysSoftCheck_Status_{CheckEligibilityStatus.parentNotFound}");
        if (notFoundRetentionDays > 0)
        {
            var checkDate = DateTime.UtcNow.AddDays(
                -notFoundRetentionDays);
            var items = _db.CheckEligibilities.Where(x => x.Created <= checkDate);
            _db.CheckEligibilities.RemoveRange(items);
            await _db.SaveChangesAsync();
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Use of bulk operations")]
    public async Task ImportEstablishments(IEnumerable<EstablishmentRow> data)
    {
        //remove records where la is 0
        data = data.Where(x => x.LaCode != 0).ToList();

        var localAuthorites = data
            .Select(m => new { m.LaCode, m.LaName })
            .Distinct()
            .Select(x => new LocalAuthority { LocalAuthorityId = x.LaCode, LaName = x.LaName });

        foreach (var la in localAuthorites)
        {
            var item = _db.LocalAuthorities.FirstOrDefault(x => x.LocalAuthorityId == la.LocalAuthorityId);
            if (item != null)
                try
                {
                    SetLaData(la);
                }
                catch (Exception ex)
                {
                    _logger.LogError("db error", ex);
                }

            else
                _db.LocalAuthorities.Add(la);
        }

        await _db.SaveChangesAsync();

        var Establishment = data.Select(x => new Establishment
        {
            EstablishmentId = x.Urn,
            EstablishmentName = x.EstablishmentName,
            LocalAuthorityId = x.LaCode,
            Locality = x.Locality,
            Postcode = x.Postcode,
            StatusOpen = x.Status == "Open",
            Street = x.Street,
            Town = x.Town,
            County = x.County,
            Type = x.Type
        });

        try
        {
            foreach (var sc in Establishment)
            {
                var item = await _db.Establishments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.EstablishmentId == sc.EstablishmentId);

                if (item != null)
                    try
                    {
                        SetEstablishmentData(sc);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"db error {item.EstablishmentId} {item.EstablishmentName}", ex);
                    }
                else
                    _db.Establishments.Add(sc);
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
            //fall through
        }
    }

    public async Task ImportMats(IEnumerable<MatRow> data) //TODO: Split out into separate functions?
    {
        var multiAcademyTrusts = data
            .Select(m => new { m.GroupUID, m.GroupName })
            .Distinct()
            .Select(x => new MultiAcademyTrust { UID = x.GroupUID, Name = x.GroupName });

        //TODO: Write all of these to db, removing existing records

        var multiAcademyTrustSchools = data
            .Select(x => new MultiAcademyTrustSchool { TrustId = x.GroupUID, SchoolId = x.AcademyURN });

        //TODO: Write all of these to db, removing existing records
    }


    public async Task ImportHMRCData(IEnumerable<FreeSchoolMealsHMRC> data)
    {
        _db.BulkInsert_FreeSchoolMealsHMRC(data);
    }

    public async Task ImportHomeOfficeData(IEnumerable<FreeSchoolMealsHO> data)
    {
        _db.BulkInsert_FreeSchoolMealsHO(data);
    }

    public async Task ImportWfHMRCData(IEnumerable<WorkingFamiliesEvent> data)
    {
        // Don't insert exact duplicates
        var codesToInsert = data.Select(x => x.EligibilityCode).ToList();
        var codeEvents = await _db.WorkingFamiliesEvents.Where(x => codesToInsert.Contains(x.EligibilityCode)).ToListAsync();
        var codeHashes = codeEvents.Select(x => x.getHash());
        data = data.Where(x => !codeHashes.Contains(x.getHash()));
        _db.BulkInsert_WorkingFamiliesEvent(data);
    }

    [ExcludeFromCodeCoverage(Justification =
        "In memory db does not support execute update, direct updating causes concurrency error")]
    private void SetLaData(LocalAuthority? item)
    {
        _db.LocalAuthorities.AsNoTracking().Where(b => b.LocalAuthorityId == item.LocalAuthorityId)
            .ExecuteUpdate(setters => setters
                .SetProperty(b => b.LaName, item.LaName));
    }

    [ExcludeFromCodeCoverage(Justification =
        "In memory db does not support execute update, direct updating causes concurrency error")]
    private void SetEstablishmentData(Establishment? item)
    {
        _db.Establishments.Where(b => b.EstablishmentId == item.EstablishmentId)
            .ExecuteUpdate(setters => setters
                .SetProperty(b => b.LocalAuthorityId, item.LocalAuthorityId)
                .SetProperty(b => b.EstablishmentName, item.EstablishmentName)
                .SetProperty(b => b.Street, item.Street)
                .SetProperty(b => b.Postcode, item.Postcode)
                .SetProperty(b => b.County, item.County)
                .SetProperty(b => b.StatusOpen, item.StatusOpen)
                .SetProperty(b => b.Locality, item.Locality)
                .SetProperty(b => b.Town, item.Town)
                .SetProperty(b => b.Type, item.Type)
            );
    }
}