using CheckYourEligibility.API.Domain;
using Microsoft.EntityFrameworkCore;

public interface IEligibilityCheckContext
{
    DbSet<WorkingFamiliesEvent> WorkingFamiliesEvents { get; set; }
    DbSet<EligibilityCheck> CheckEligibilities { get; set; }
    DbSet<FreeSchoolMealsHMRC> FreeSchoolMealsHMRC { get; set; }
    DbSet<FreeSchoolMealsHO> FreeSchoolMealsHO { get; set; }
    DbSet<Establishment> Establishments { get; set; }
    DbSet<LocalAuthority> LocalAuthorities { get; set; }
    DbSet<Application> Applications { get; set; }
    DbSet<ApplicationStatus> ApplicationStatuses { get; set; }
    DbSet<EligibilityCheckHash> EligibilityCheckHashes { get; set; }
    DbSet<User> Users { get; set; }
    DbSet<Audit> Audits { get; set; }    void BulkInsert_FreeSchoolMealsHO(IEnumerable<FreeSchoolMealsHO> data);
    Task<int> SaveChangesAsync();
    int SaveChanges();
    void BulkInsert_FreeSchoolMealsHMRC(IEnumerable<FreeSchoolMealsHMRC> data);
    void BulkInsert_Applications(IEnumerable<Application> data);
}