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

    public virtual DbSet<WorkingFamiliesEvent> WorkingFamiliesEvents { get; set; }
    public virtual DbSet<ApplicationEvidence> ApplicationEvidence { get; set; }
    public virtual DbSet<EligibilityCheck> CheckEligibilities { get; set; }
    public virtual DbSet<FreeSchoolMealsHMRC> FreeSchoolMealsHMRC { get; set; }
    public virtual DbSet<FreeSchoolMealsHO> FreeSchoolMealsHO { get; set; }
    public virtual DbSet<Establishment> Establishments { get; set; }
    public virtual DbSet<LocalAuthority> LocalAuthorities { get; set; }
    public virtual DbSet<Application> Applications { get; set; }
    public virtual DbSet<ApplicationStatus> ApplicationStatuses { get; set; }
    public virtual DbSet<EligibilityCheckHash> EligibilityCheckHashes { get; set; }
    public virtual DbSet<RateLimitEvent> RateLimitEvents { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Audit> Audits { get; set; }


    public Task<int> SaveChangesAsync()
    {
        return base.SaveChangesAsync();
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

        modelBuilder.Entity<Establishment>()
            .HasOne(e => e.LocalAuthority);

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


        modelBuilder.Entity<EligibilityCheckHash>()
            .HasIndex(b => b.Hash, "idx_EligibilityCheckHash");

        //TODO: Likely need to index the RateLimitEvents by partitionName and timestamp

        modelBuilder.Entity<User>()
            .HasIndex(p => new { p.Email, p.Reference }).IsUnique();
    }
}