// Ignore Spelling: Fsm

using System.Diagnostics.CodeAnalysis;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using ApplicationStatus = CheckYourEligibility.API.Domain.ApplicationStatus;

[ExcludeFromCodeCoverage(Justification = "framework")]
public class EligibilityCheckContext : DbContext, IEligibilityCheckContext
{
    public EligibilityCheckContext(DbContextOptions<EligibilityCheckContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (EF.IsDesignTime)
        {
            optionsBuilder.UseSqlServer(opt => opt.CommandTimeout(3600));
        }
        base.OnConfiguring(optionsBuilder);
    }

    public virtual DbSet<ECSConflict> ECSConflicts { get; set; }
    public virtual DbSet<WorkingFamiliesEvent> WorkingFamiliesEvents { get; set; }
    public virtual DbSet<ApplicationEvidence> ApplicationEvidence { get; set; }
    public virtual DbSet<EligibilityCheck> CheckEligibilities { get; set; }
    public virtual DbSet<BulkCheck> BulkChecks { get; set; }
    public virtual DbSet<FreeSchoolMealsHMRC> FreeSchoolMealsHMRC { get; set; }
    public virtual DbSet<FreeSchoolMealsHO> FreeSchoolMealsHO { get; set; }
    public virtual DbSet<Establishment> Establishments { get; set; }
    public virtual DbSet<MultiAcademyTrust> MultiAcademyTrusts { get; set; }
    public virtual DbSet<MultiAcademyTrustEstablishment> MultiAcademyTrustEstablishments { get; set; }
    public virtual DbSet<LocalAuthority> LocalAuthorities { get; set; }
    public virtual DbSet<Application> Applications { get; set; }
    public virtual DbSet<ApplicationStatus> ApplicationStatuses { get; set; }
    public virtual DbSet<EligibilityCheckHash> EligibilityCheckHashes { get; set; }
    public virtual DbSet<RateLimitEvent> RateLimitEvents { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Audit> Audits { get; set; }
    public virtual DbSet<FosterCarer> FosterCarers { get; set; }
    public virtual DbSet<FosterChild> FosterChildren { get; set; }

    public Task<int> SaveChangesAsync()
    {
        
        return base.SaveChangesAsync();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();   
        return base.SaveChangesAsync();
    }

    /// <summary>
    /// Automatically sets audit fields for any entity implementing IAuditable.
    /// - When an entity is Added: sets Created and Updated to the current UTC timestamp.
    /// - When an entity is Modified: sets Updated to the current UTC timestamp.
    /// - No timestamps are changed if the entity has no tracked modifications.
    /// </summary>
    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = now;
                    entry.Entity.Updated = now;
                    break;

                case EntityState.Modified:
                    // Only entities with actual property diffs get here
                    entry.Entity.Updated = now;
                    break;
            }
        }
    }

    public void BulkInsert_FreeSchoolMealsHO(IEnumerable<FreeSchoolMealsHO> data)
    {
        using var transaction = base.Database.BeginTransaction();
        this.Truncate<FreeSchoolMealsHO>();
        this.BulkInsert(data);
        transaction.Commit();
    }

    int IEligibilityCheckContext.SaveChanges()
    {
        return base.SaveChanges();
    }

    public void BulkInsert_FreeSchoolMealsHMRC(IEnumerable<FreeSchoolMealsHMRC> data)
    {
        using var transaction = base.Database.BeginTransaction();
        this.Truncate<FreeSchoolMealsHMRC>();
        this.BulkInsert(data);
        transaction.Commit();
    }

    public void BulkInsert_Applications(IEnumerable<Application> data)
    {
        this.BulkInsert(data);
    }

    public void BulkInsert_WorkingFamiliesEvent(IEnumerable<WorkingFamiliesEvent> data)
    {
        this.BulkInsert(data);
    }
    public void BulkInsertOrUpdate_LocalAuthority(IEnumerable<LocalAuthority> data)
    {

        using var transaction = base.Database.BeginTransaction();
        try
        {
            this.BulkInsertOrUpdate(data, config =>
            config.UpdateByProperties = new List<string> { nameof(LocalAuthority.LocalAuthorityID), nameof(LocalAuthority.LaName) });
            transaction.Commit();
        }

        catch (Exception ex)
        {

            transaction.Rollback();
        }

    }
    public void BulkInsertOrUpdate_Establishment(IEnumerable<Establishment> data)
    {

        using var transaction = base.Database.BeginTransaction();

        try
        {
            this.BulkInsertOrUpdate(data, config =>
            {
                config.BatchSize = 30000;
                config.PropertiesToExcludeOnUpdate = new List<string> { nameof(Establishment.InPrivateBeta) };
            });
            transaction.Commit();

        }
        catch (Exception ex)
        {
            transaction.Rollback();
        }


    }
    public void BulkInsert_MultiAcademyTrusts(IEnumerable<MultiAcademyTrust> trustData, IEnumerable<MultiAcademyTrustEstablishment> schoolData)
    {
        using (var transaction = base.Database.BeginTransaction())
        {
            this.Truncate<MultiAcademyTrustEstablishment>();
            this.MultiAcademyTrusts.ExecuteDelete();
            this.BulkInsert(trustData);
            this.BulkInsert(schoolData);
            transaction.Commit();
        }
    }



    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<EligibilityCheck>().ToTable("EligibilityCheck");
        modelBuilder.Entity<EligibilityCheck>()
            .Property(p => p.Status)
            .HasConversion(
                v => v.ToString(),
                v => (CheckEligibilityStatus)Enum.Parse(typeof(CheckEligibilityStatus), v));
        modelBuilder.Entity<EligibilityCheck>()
            .Property(p => p.Type)
            .HasConversion(
                v => v.ToString(),
                v => (CheckEligibilityType)Enum.Parse(typeof(CheckEligibilityType), v));

        // BulkCheck configuration
        modelBuilder.Entity<BulkCheck>()
            .HasKey(b => b.BulkCheckID);
        modelBuilder.Entity<BulkCheck>()
            .Property(p => p.EligibilityType)
            .HasConversion(
                v => v.ToString(),
                v => (CheckEligibilityType)Enum.Parse(typeof(CheckEligibilityType), v));

        // BulkCheck to LocalAuthority relationship
        modelBuilder.Entity<BulkCheck>()
            .HasOne(b => b.LocalAuthority)
            .WithMany()
            .HasForeignKey(b => b.LocalAuthorityID)
            .IsRequired(false);


        // EligibilityCheck to BulkCheck relationship
        modelBuilder.Entity<EligibilityCheck>()
            .HasOne(e => e.BulkCheck)
            .WithMany(b => b.EligibilityChecks)
            .HasForeignKey(e => e.BulkCheckID)
            .HasPrincipalKey(b => b.BulkCheckID)
            .IsRequired(false);

        modelBuilder.Entity<Establishment>()
            .HasOne(e => e.LocalAuthority);

        // MultiAcademyTrustSchool to MultiAcademyTrust relationship
        modelBuilder.Entity<MultiAcademyTrustEstablishment>()
            .HasOne(s => s.MultiAcademyTrust)
            .WithMany(t => t.MultiAcademyTrustEstablishments)
            .HasForeignKey(s => s.MultiAcademyTrustID)
            .HasPrincipalKey(t => t.MultiAcademyTrustID)
            .IsRequired(true);

        modelBuilder.Entity<Application>()
            .HasIndex(b => b.Reference, "idx_Reference")
            .IsUnique();
        modelBuilder.Entity<Application>()
            .HasIndex(b => b.Status, "idx_ApplicationStatus");

        modelBuilder.Entity<ApplicationEvidence>()
            .HasOne(e => e.Application)
            .WithMany(a => a.Evidence)
            .HasForeignKey(e => e.ApplicationID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApplicationEvidence>()
            .HasIndex(e => e.ApplicationID, "idx_ApplicationEvidence_ApplicationID");

        modelBuilder.Entity<Audit>()
            .HasIndex(a => a.TypeID, "idx_TypeId");

        modelBuilder.Entity<Audit>()
            .HasIndex(a => a.Method, "idx_Method")
            .HasFilter("[Method] = 'POST' AND [Type] = 'Check'");

        modelBuilder.Entity<EligibilityCheckHash>()
            .HasIndex(b => b.Hash, "idx_EligibilityCheckHash");

        modelBuilder.Entity<User>()
            .HasIndex(p => new { p.Email, p.Reference }).IsUnique();

        modelBuilder.Entity<FosterChild>()
            .HasOne(fc => fc.FosterCarer)
            .WithOne(c => c.FosterChild)
            .HasForeignKey<FosterChild>(fc => fc.FosterCarerId)
            .IsRequired();
        
        
        modelBuilder.Entity<FosterChild>()
            .HasIndex(fc => fc.EligibilityCode);
       
        modelBuilder.Entity<WorkingFamiliesEvent>()
            .HasIndex(e => e.EligibilityCode);



    }
}
        
        