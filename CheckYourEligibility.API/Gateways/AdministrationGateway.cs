// Ignore Spelling: Fsm

using System.Diagnostics;
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

    //TODO: This should live in the Establishment gateway
    [ExcludeFromCodeCoverage(Justification = "Use of bulk operations")]
    public async Task ImportEstablishments(IEnumerable<EstablishmentRow> data)
    {
        try
        {
            //remove records where la is 0
            data = data.Where(x => x.LaCode != 0).ToList();

        var localAuthorities = data
            .Select(m => new { m.LaCode, m.LaName })
            .Distinct()
            .Select(x => new LocalAuthority { LocalAuthorityID = x.LaCode, LaName = x.LaName });

        _db.BulkInsertOrUpdate_LocalAuthority(localAuthorities);

        var Establishments = data.Select(x => new Establishment
        {
            EstablishmentID = x.Urn,
            EstablishmentName = x.EstablishmentName,
            LocalAuthorityID = x.LaCode,
            Locality = x.Locality,
            Postcode = x.Postcode,
            StatusOpen = x.Status == "Open",
            Street = x.Street,
            Town = x.Town,
            County = x.County,
            Type = x.Type
        });
     
            var establishmentList = Establishments.ToList();
            int total = establishmentList.Count();
            const int batchSize = 3000;
            int batchNo = 1;
            for (int offset = 0; offset < total; offset += batchSize)
            {
                var batch = establishmentList.Skip(offset).Take(batchSize);
                _db.BulkInsertOrUpdate_Establishment(batch);
                batchNo++;           
            }       
        }
        catch (Exception ex)
        {
            //fall through
        }
    }

    //TODO: This should live in its own MAT gateway
    public async Task ImportMats(IEnumerable<MatRow> data)
    {
        var multiAcademyTrusts = data
            .Select(m => new { m.GroupUID, m.GroupName })
            .Distinct()
            .Select(x => new MultiAcademyTrust { MultiAcademyTrustID = x.GroupUID, Name = x.GroupName });

        var multiAcademyTrustEstablishments = data
            .Select(x => new MultiAcademyTrustEstablishment { MultiAcademyTrustID = x.GroupUID, EstablishmentID = x.AcademyURN });

        _db.BulkInsert_MultiAcademyTrusts(multiAcademyTrusts, multiAcademyTrustEstablishments);
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
    public async Task UpdateEstablishmentsPrivateBeta(IEnumerable<EstablishmentPrivateBetaRow> data)
    {
        foreach (var item in data)
        {
            _db.Establishments.Where(b => b.EstablishmentID == item.EstablishmentId)
                .ExecuteUpdate(setters => setters
                    .SetProperty(b => b.InPrivateBeta, item.InPrivateBeta));
        }

        await _db.SaveChangesAsync();
    }
}